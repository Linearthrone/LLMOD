"""
Reference cognitive agent implementation for House Victoria MCP.

This module sketches a modular cognitive agent with the following components:
- PerceptionSystem
- WorldModel
- MemorySystem (wrapping the existing MemoryManager)
- DriveSystem
- GoalGenerator
- LLMPlanner (designed to call tools / workflows)
- ToolRouter (maps plans to concrete MCP tools)
- ReflectionEngine
- CognitiveAgent controller with a `step` method

The design is host-agnostic: the agent is constructed with dependencies
from the MCP server (MemoryManager, WorkflowEngine, tool executor) rather
than creating them internally.
"""

from __future__ import annotations

import random
from dataclasses import dataclass, field
from typing import Any, Awaitable, Callable, Dict, List, Optional, Protocol, Tuple

from ..memory.memory_manager import MemoryManager
from ..tt.workflow_engine import WorkflowEngine


class ToolExecutor(Protocol):
    """Abstract tool executor used by ToolRouter.

    Implementations can wrap:
    - the in-process `_tool_functions` registry in `server.py`
    - external HTTP / MCP tool calls
    """

    async def __call__(self, tool_name: str, **kwargs: Any) -> Any:  # pragma: no cover - protocol
        ...


LOOP_INTERVAL_SECONDS: float = 2.0


@dataclass
class PerceptionOutput:
    """Structured perception output."""

    player_visible: bool
    player_distance: float
    objects: List[str]
    speech_input: Optional[str] = None


class PerceptionSystem:
    """Perception system for the agent.

    In the reference implementation this is mostly a stub that can be
    replaced with:
    - Unreal scene snapshots
    - system / sensor readings
    - STT transcription, etc.
    """

    async def collect(self, external_input: Optional[Dict[str, Any]] = None) -> PerceptionOutput:
        """Collect perception data.

        Args:
            external_input: Optional structured input from the host.
        """
        if external_input:
            return PerceptionOutput(
                player_visible=bool(external_input.get("player_visible", False)),
                player_distance=float(external_input.get("player_distance", 0.0)),
                objects=list(external_input.get("objects", [])),
                speech_input=external_input.get("speech_input"),
            )

        # Fallback: simple randomized environment (reference skeleton)
        return PerceptionOutput(
            player_visible=random.choice([True, False]),
            player_distance=round(random.uniform(1.0, 4.0), 2),
            objects=["cup", "chair", "table"],
            speech_input=None,
        )


@dataclass
class WorldState:
    """World model state."""

    player_visible: bool = False
    player_distance: float = 0.0
    objects: List[str] = field(default_factory=list)
    last_action: Optional[Dict[str, Any]] = None


class WorldModel:
    """Simple world model that tracks recent perception and actions."""

    def __init__(self) -> None:
        self.state = WorldState()

    def update(self, perception: PerceptionOutput, last_action: Optional[Dict[str, Any]] = None) -> WorldState:
        self.state.player_visible = perception.player_visible
        self.state.player_distance = perception.player_distance
        self.state.objects = perception.objects
        if last_action is not None:
            self.state.last_action = last_action
        return self.state


class MemorySystem:
    """Working/episodic memory abstraction on top of MemoryManager."""

    def __init__(self, memory_manager: MemoryManager) -> None:
        self.memory_manager = memory_manager
        self.working_memory: List[Dict[str, Any]] = []
        self.episodic_keys: List[str] = []

    async def store_working(self, event: Dict[str, Any]) -> None:
        self.working_memory.append(event)
        if len(self.working_memory) > 10:
            self.working_memory.pop(0)

    async def store_episode(self, event: Dict[str, Any], category: str = "episode") -> str:
        key = await self.memory_manager.remember(
            value=event,
            category=category,
            importance=1.0,
        )
        self.episodic_keys.append(key)
        return key

    def retrieve_recent_working(self, limit: int = 5) -> List[Dict[str, Any]]:
        return self.working_memory[-limit:]


class DriveSystem:
    """Simple drive system tracking social, curiosity, and boredom."""

    def __init__(self) -> None:
        self.drives: Dict[str, float] = {
            "social": 0.5,
            "curiosity": 0.5,
            "boredom": 0.2,
        }

    def update(self, world_state: WorldState) -> Dict[str, float]:
        if world_state.player_visible:
            self.drives["social"] += 0.05
            self.drives["boredom"] = max(0.0, self.drives["boredom"] - 0.02)
        else:
            self.drives["curiosity"] += 0.03
            self.drives["boredom"] += 0.02

        self._normalize()
        return self.drives

    def _normalize(self) -> None:
        for k in self.drives:
            self.drives[k] = max(0.0, min(self.drives[k], 1.0))


class GoalGenerator:
    """Goal generator based on drives and world state."""

    def generate(self, drives: Dict[str, float], world_state: WorldState) -> List[Tuple[str, float]]:
        goals: List[Tuple[str, float]] = []

        if drives["social"] > 0.6 and world_state.player_visible:
            goals.append(("greet_player", 0.9))

        if drives["curiosity"] > 0.6 and world_state.objects:
            goals.append(("inspect_object", 0.6))

        if drives["boredom"] > 0.5:
            goals.append(("wander", 0.5))

        return goals

    def select(self, goals: List[Tuple[str, float]]) -> str:
        if not goals:
            return "idle"
        goals_sorted = sorted(goals, key=lambda g: g[1], reverse=True)
        return goals_sorted[0][0]


class LLMPlanner:
    """Planner abstraction that creates a simple plan for a goal.

    In the full system this could:
    - call an LLM via MCP or the Ollama service
    - create and execute TT workflows
    For now we keep the logic local and deterministic.
    """

    async def create_plan(
        self,
        goal: str,
        world_state: WorldState,
        recent_memories: List[Dict[str, Any]],
    ) -> Dict[str, Any]:
        if goal == "greet_player":
            return {"tool": "wave"}

        if goal == "inspect_object" and world_state.objects:
            obj = random.choice(world_state.objects)
            return {"tool": "look_at", "target": obj}

        if goal == "wander":
            return {"tool": "move_random"}

        return {"tool": "idle"}


class ToolRouter:
    """Map plans to concrete tool calls or simple side effects."""

    def __init__(self, tool_executor: Optional[ToolExecutor] = None) -> None:
        self.tool_executor = tool_executor

    async def execute(self, plan: Dict[str, Any]) -> Dict[str, Any]:
        tool = plan.get("tool", "idle")

        # If a ToolExecutor is provided, prefer named tools that exist in the host.
        if self.tool_executor is not None and tool not in {"idle"}:
            return await self.tool_executor(tool_name=tool, **{k: v for k, v in plan.items() if k != "tool"})

        # Fallback: local behavior
        if tool == "wave":
            return await self._wave()
        if tool == "look_at":
            return await self._look_at(plan.get("target"))
        if tool == "move_random":
            return await self._move_random()

        return {"result": "idle"}

    async def _wave(self) -> Dict[str, Any]:
        print("Agent waves.")
        return {"result": "wave"}

    async def _look_at(self, target: Optional[str]) -> Dict[str, Any]:
        print(f"Agent looks at {target}")
        return {"result": "look_at", "target": target}

    async def _move_random(self) -> Dict[str, Any]:
        print("Agent wanders around.")
        return {"result": "wander"}


class ReflectionEngine:
    """Periodic reflection over recent memories."""

    def __init__(self, interval_steps: int = 10) -> None:
        self.interval_steps = interval_steps
        self._counter = 0

    async def maybe_reflect(self, memory: MemorySystem) -> Optional[Dict[str, Any]]:
        self._counter += 1
        if self._counter % self.interval_steps != 0:
            return None

        recent = memory.retrieve_recent_working()
        summary = {
            "type": "reflection",
            "recent_events": recent,
        }
        print("Reflection: analyzing recent experiences")
        print(summary)
        return summary


class CognitiveAgent:
    """High-level cognitive agent controller."""

    def __init__(
        self,
        memory_manager: MemoryManager,
        workflow_engine: Optional[WorkflowEngine] = None,
        tool_executor: Optional[ToolExecutor] = None,
        name: str = "Ava",
        personality: Optional[Dict[str, float]] = None,
    ) -> None:
        self.name = name
        self.personality = personality or {
            "curiosity": 0.8,
            "kindness": 0.7,
            "humor": 0.5,
            "confidence": 0.4,
        }

        self.perception = PerceptionSystem()
        self.world_model = WorldModel()
        self.memory = MemorySystem(memory_manager)
        self.drives = DriveSystem()
        self.goal_system = GoalGenerator()
        self.planner = LLMPlanner()
        self.tools = ToolRouter(tool_executor=tool_executor)
        self.reflection = ReflectionEngine()
        self.workflow_engine = workflow_engine

    async def step(self, external_input: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Run a single cognitive step.

        This is designed to be called by the MCP server or host app.
        """
        perception_data = await self.perception.collect(external_input)

        # Update world model
        world_state = self.world_model.update(perception_data)

        # Store in working memory
        await self.memory.store_working(
            {
                "world_state": world_state.__dict__.copy(),
                "personality": self.personality.copy(),
            }
        )

        # Update drives and generate goals
        drives = self.drives.update(world_state)
        goals = self.goal_system.generate(drives, world_state)
        goal = self.goal_system.select(goals)

        # Retrieve recent working memory and create a plan
        recent_memories = self.memory.retrieve_recent_working()
        plan = await self.planner.create_plan(goal, world_state, recent_memories)

        # Execute the plan via tools
        result = await self.tools.execute(plan)

        # Store episode in persistent memory
        await self.memory.store_episode(
            {
                "goal": goal,
                "plan": plan,
                "result": result,
                "drives": drives.copy(),
                "world_state": world_state.__dict__.copy(),
            }
        )

        # Periodic reflection
        reflection = await self.reflection.maybe_reflect(self.memory)

        return {
            "goal": goal,
            "plan": plan,
            "result": result,
            "drives": drives,
            "world_state": world_state.__dict__,
            "reflection": reflection,
        }

