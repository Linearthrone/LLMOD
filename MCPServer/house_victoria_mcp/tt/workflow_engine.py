"""Workflow engine for TT (Task & Workflow) functionality."""

import asyncio
from enum import Enum
from typing import Any, Callable, Dict, List, Optional
from dataclasses import dataclass, field

from .task_manager import TaskManager, TaskStatus, TaskPriority
from ..logger import get_logger

logger = get_logger("tt.workflow_engine")


class WorkflowStatus(str, Enum):
    """Workflow status enumeration."""
    PENDING = "pending"
    RUNNING = "running"
    PAUSED = "paused"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class WorkflowStepType(str, Enum):
    """Workflow step type enumeration."""
    TASK = "task"
    CONDITION = "condition"
    PARALLEL = "parallel"
    DELAY = "delay"
    TOOL_CALL = "tool_call"


@dataclass
class WorkflowStep:
    """Workflow step definition."""
    
    id: str
    name: str
    step_type: WorkflowStepType
    step_config: Dict[str, Any]
    depends_on: List[str] = field(default_factory=list)
    status: TaskStatus = TaskStatus.PENDING
    result: Optional[Any] = None
    error: Optional[str] = None


@dataclass
class Workflow:
    """Workflow definition."""
    
    id: str
    name: str
    description: str
    steps: List[WorkflowStep] = field(default_factory=list)
    status: WorkflowStatus = WorkflowStatus.PENDING
    created_at: float = field(default_factory=lambda: asyncio.get_event_loop().time())
    started_at: Optional[float] = None
    completed_at: Optional[float] = None
    progress: float = 0.0
    metadata: Dict[str, Any] = field(default_factory=dict)


class WorkflowEngine:
    """Engine for executing workflows."""

    def __init__(self, task_manager: Optional[TaskManager] = None):
        """Initialize workflow engine.

        Args:
            task_manager: Task manager instance (creates new if None)
        """
        self.task_manager = task_manager or TaskManager()
        self.workflows: Dict[str, Workflow] = {}
        self._running_workflows: Dict[str, asyncio.Task] = {}

    async def create_workflow(
        self,
        name: str,
        description: str,
        steps: List[WorkflowStep],
        metadata: Optional[Dict[str, Any]] = None,
    ) -> Workflow:
        """Create a new workflow.

        Args:
            name: Workflow name
            description: Workflow description
            steps: List of workflow steps
            metadata: Additional metadata

        Returns:
            Created workflow
        """
        workflow_id = self._generate_workflow_id(name)
        
        workflow = Workflow(
            id=workflow_id,
            name=name,
            description=description,
            steps=steps,
            metadata=metadata or {},
        )
        
        self.workflows[workflow_id] = workflow
        logger.info(f"Created workflow: {workflow_id} - {name}")
        return workflow

    async def get_workflow(self, workflow_id: str) -> Optional[Workflow]:
        """Get a workflow by ID.

        Args:
            workflow_id: Workflow ID

        Returns:
            Workflow or None if not found
        """
        return self.workflows.get(workflow_id)

    async def execute_workflow(
        self,
        workflow_id: str,
        tool_registry: Optional[Dict[str, Callable]] = None,
    ) -> Workflow:
        """Execute a workflow.

        Args:
            workflow_id: Workflow ID
            tool_registry: Registry of available tool functions

        Returns:
            Updated workflow
        """
        workflow = self.workflows.get(workflow_id)
        if not workflow:
            raise ValueError(f"Workflow not found: {workflow_id}")
        
        workflow.status = WorkflowStatus.RUNNING
        workflow.started_at = asyncio.get_event_loop().time()
        
        # Create execution task
        self._running_workflows[workflow_id] = asyncio.create_task(
            self._execute_workflow_task(workflow, tool_registry or {})
        )
        
        # Wait for completion (or handle asynchronously)
        await self._running_workflows[workflow_id]
        
        return workflow

    async def _execute_workflow_task(
        self,
        workflow: Workflow,
        tool_registry: Dict[str, Callable],
    ) -> None:
        """Execute workflow task.

        Args:
            workflow: Workflow to execute
            tool_registry: Available tools
        """
        try:
            # Execute steps in order
            steps_executed = 0
            
            for step in workflow.steps:
                # Check dependencies
                for dep_id in step.depends_on:
                    dep_step = self._get_step_by_id(workflow, dep_id)
                    if not dep_step or dep_step.status != TaskStatus.COMPLETED:
                        logger.warning(f"Step {step.id} waiting for dependency {dep_id}")
                        await asyncio.sleep(1)  # Poll for dependency
                        continue
                
                # Execute step
                await self._execute_step(step, tool_registry)
                workflow.progress = (steps_executed + 1) / len(workflow.steps) * 100
                steps_executed += 1
            
            workflow.status = WorkflowStatus.COMPLETED
            workflow.completed_at = asyncio.get_event_loop().time()
            logger.info(f"Workflow {workflow.id} completed")
            
        except asyncio.CancelledError:
            workflow.status = WorkflowStatus.CANCELLED
            logger.info(f"Workflow {workflow.id} cancelled")
            
        except Exception as e:
            workflow.status = WorkflowStatus.FALED
            logger.error(f"Workflow {workflow.id} failed: {str(e)}")
            
        finally:
            del self._running_workflows[workflow.id]

    async def _execute_step(
        self,
        step: WorkflowStep,
        tool_registry: Dict[str, Callable],
    ) -> Any:
        """Execute a workflow step.

        Args:
            step: Step to execute
            tool_registry: Available tools

        Returns:
            Step result
        """
        step.status = TaskStatus.IN_PROGRESS
        
        try:
            if step.step_type == WorkflowStepType.TASK:
                # Create and execute task
                task = await self.task_manager.create_task(
                    name=step.name,
                    description=step.step_config.get("description", ""),
                    priority=TaskPriority.MEDIUM,
                    metadata={"step_id": step.id, "workflow": True},
                )
                
                # Execute with provided function if available
                if "execution_func" in step.step_config:
                    result = await self.task_manager.execute_task(
                        task.id,
                        step.step_config["execution_func"],
                    )
                    step.result = result
                else:
                    # Just create the task
                    step.result = {"task_id": task.id}
                
                step.status = TaskStatus.COMPLETED
                
            elif step.step_type == WorkflowStepType.TOOL_CALL:
                tool_name = step.step_config["tool_name"]
                tool_args = step.step_config.get("tool_args", {})
                
                if tool_name not in tool_registry:
                    raise ValueError(f"Tool not found: {tool_name}")
                
                tool_func = tool_registry[tool_name]
                step.result = await tool_func(**tool_args)
                step.status = TaskStatus.COMPLETED
                
            elif step.step_type == WorkflowStepType.DELAY:
                delay_seconds = step.step_config.get("seconds", 1.0)
                await asyncio.sleep(delay_seconds)
                step.result = {"delayed": delay_seconds}
                step.status = TaskStatus.COMPLETED
                
            elif step.step_type == WorkflowStepType.CONDITION:
                condition_func = step.step_config.get("condition")
                if not condition_func:
                    raise ValueError("Condition function not provided")
                
                result = condition_func(step.step_config.get("context", {}))
                step.result = {"condition_result": result}
                step.status = TaskStatus.COMPLETED
                
            elif step.step_type == WorkflowStepType.PARALLEL:
                # Execute multiple steps in parallel
                parallel_steps = step.step_config.get("steps", [])
                
                async def execute_parallel_step(s):
                    sub_step = WorkflowStep(
                        id=f"{step.id}_{s['id']}",
                        name=s.get("name", "Parallel Step"),
                        step_type=s.get("type", WorkflowStepType.TASK),
                        step_config=s.get("config", {}),
                    )
                    return await self._execute_step(sub_step, tool_registry)
                
                results = await asyncio.gather(*[
                    execute_parallel_step(s) for s in parallel_steps
                ])
                step.result = {"parallel_results": results}
                step.status = TaskStatus.COMPLETED
                
            logger.info(f"Step {step.id} completed")
            
        except Exception as e:
            step.status = TaskStatus.FAILED
            step.error = str(e)
            logger.error(f"Step {step.id} failed: {str(e)}")
            raise

    def _get_step_by_id(self, workflow: Workflow, step_id: str) -> Optional[WorkflowStep]:
        """Get a step by ID.

        Args:
            workflow: Workflow to search
            step_id: Step ID

        Returns:
            Step or None if not found
        """
        for step in workflow.steps:
            if step.id == step_id:
                return step
        return None

    async def pause_workflow(self, workflow_id: str) -> bool:
        """Pause a running workflow.

        Args:
            workflow_id: Workflow ID

        Returns:
            True if paused, False otherwise
        """
        if workflow_id in self._running_workflows:
            self._running_workflows[workflow_id].cancel()
            workflow = self.workflows.get(workflow_id)
            if workflow:
                workflow.status = WorkflowStatus.PAUSED
            return True
        return False

    async def cancel_workflow(self, workflow_id: str) -> bool:
        """Cancel a workflow.

        Args:
            workflow_id: Workflow ID

        Returns:
            True if cancelled, False otherwise
        """
        if workflow_id in self._running_workflows:
            self._running_workflows[workflow_id].cancel()
            workflow = self.workflows.get(workflow_id)
            if workflow:
                workflow.status = WorkflowStatus.CANCELLED
            return True
        return False

    async def list_workflows(
        self,
        status: Optional[WorkflowStatus] = None,
    ) -> List[Workflow]:
        """List workflows with optional filter.

        Args:
            status: Filter by status

        Returns:
            Filtered list of workflows
        """
        workflows = list(self.workflows.values())
        
        if status:
            workflows = [w for w in workflows if w.status == status]
        
        return workflows

    async def delete_workflow(self, workflow_id: str) -> bool:
        """Delete a workflow.

        Args:
            workflow_id: Workflow ID

        Returns:
            True if deleted, False if not found
        """
        if workflow_id in self.workflows:
            del self.workflows[workflow_id]
            logger.info(f"Deleted workflow: {workflow_id}")
            return True
        return False

    def _generate_workflow_id(self, name: str) -> str:
        """Generate a unique workflow ID.

        Args:
            name: Workflow name

        Returns:
            Unique workflow ID
        """
        import hashlib
        timestamp = asyncio.get_event_loop().time()
        combined = f"{name}_{timestamp}"
        return hashlib.sha256(combined.encode()).hexdigest()[:16]
