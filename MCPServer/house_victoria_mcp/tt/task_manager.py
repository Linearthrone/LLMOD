"""Task management for TT (Task & Workflow) functionality."""

import asyncio
import json
from datetime import datetime
from enum import Enum
from typing import Any, Dict, List, Optional
from dataclasses import dataclass, field

from ..logger import get_logger

logger = get_logger("tt.task_manager")


class TaskStatus(str, Enum):
    """Task status enumeration."""
    PENDING = "pending"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class TaskPriority(str, Enum):
    """Task priority enumeration."""
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"
    URGENT = "urgent"


@dataclass
class Task:
    """Task data structure."""
    
    id: str
    name: str
    description: str
    status: TaskStatus = TaskStatus.PENDING
    priority: TaskPriority = TaskPriority.MEDIUM
    created_at: datetime = field(default_factory=datetime.now)
    updated_at: datetime = field(default_factory=datetime.now)
    completed_at: Optional[datetime] = None
    assigned_to: Optional[str] = None
    tags: List[str] = field(default_factory=list)
    progress: float = 0.0
    result: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)
    dependencies: List[str] = field(default_factory=list)
    workflow_id: Optional[str] = None


class TaskManager:
    """Manager for task lifecycle operations."""

    def __init__(self):
        """Initialize task manager."""
        self.tasks: Dict[str, Task] = {}
        self._running_tasks: Dict[str, asyncio.Task] = {}
        
    async def create_task(
        self,
        name: str,
        description: str,
        priority: TaskPriority = TaskPriority.MEDIUM,
        assigned_to: Optional[str] = None,
        tags: Optional[List[str]] = None,
        metadata: Optional[Dict[str, Any]] = None,
        dependencies: Optional[List[str]] = None,
        workflow_id: Optional[str] = None,
    ) -> Task:
        """Create a new task.

        Args:
            name: Task name
            description: Task description
            priority: Task priority
            assigned_to: User or system assigned to
            tags: Tags for organization
            metadata: Additional metadata
            dependencies: IDs of tasks this depends on
            workflow_id: Parent workflow ID

        Returns:
            Created task
        """
        task_id = self._generate_task_id(name)
        
        task = Task(
            id=task_id,
            name=name,
            description=description,
            status=TaskStatus.PENDING,
            priority=priority,
            assigned_to=assigned_to,
            tags=tags or [],
            metadata=metadata or {},
            dependencies=dependencies or [],
            workflow_id=workflow_id,
        )
        
        self.tasks[task_id] = task
        logger.info(f"Created task: {task_id} - {name}")
        return task

    async def get_task(self, task_id: str) -> Optional[Task]:
        """Get a task by ID.

        Args:
            task_id: Task ID

        Returns:
            Task or None if not found
        """
        return self.tasks.get(task_id)

    async def update_task_status(
        self,
        task_id: str,
        status: TaskStatus,
        progress: Optional[float] = None,
        result: Optional[Dict[str, Any]] = None,
        error: Optional[str] = None,
    ) -> Optional[Task]:
        """Update task status.

        Args:
            task_id: Task ID
            status: New status
            progress: Progress percentage (0-100)
            result: Task result
            error: Error message if failed

        Returns:
            Updated task or None if not found
        """
        task = self.tasks.get(task_id)
        if not task:
            return None
        
        task.status = status
        task.updated_at = datetime.now()
        
        if progress is not None:
            task.progress = max(0.0, min(100.0, progress))
        
        if result is not None:
            task.result = result
        
        if error is not None:
            task.error = error
        
        if status == TaskStatus.COMPLETED:
            task.completed_at = datetime.now()
            task.progress = 100.0
            
            # Stop any running async task
            if task_id in self._running_tasks:
                self._running_tasks[task_id].cancel()
                del self._running_tasks[task_id]
        
        logger.info(f"Updated task {task_id}: status={status}")
        return task

    async def list_tasks(
        self,
        status: Optional[TaskStatus] = None,
        priority: Optional[TaskPriority] = None,
        assigned_to: Optional[str] = None,
        workflow_id: Optional[str] = None,
        tags: Optional[List[str]] = None,
    ) -> List[Task]:
        """List tasks with optional filters.

        Args:
            status: Filter by status
            priority: Filter by priority
            assigned_to: Filter by assignment
            workflow_id: Filter by workflow
            tags: Filter by tags

        Returns:
            Filtered list of tasks
        """
        tasks = list(self.tasks.values())
        
        if status:
            tasks = [t for t in tasks if t.status == status]
        
        if priority:
            tasks = [t for t in tasks if t.priority == priority]
        
        if assigned_to:
            tasks = [t for t in tasks if t.assigned_to == assigned_to]
        
        if workflow_id:
            tasks = [t for t in tasks if t.workflow_id == workflow_id]
        
        if tags:
            tasks = [
                t for t in tasks
                if any(tag in t.tags for tag in tags)
            ]
        
        # Sort by priority and created time
        priority_order = {TaskPriority.URGENT: 0, TaskPriority.HIGH: 1, TaskPriority.MEDIUM: 2, TaskPriority.LOW: 3}
        tasks.sort(key=lambda t: (priority_order.get(t.priority, 4), t.created_at))
        
        return tasks

    async def delete_task(self, task_id: str) -> bool:
        """Delete a task.

        Args:
            task_id: Task ID

        Returns:
            True if deleted, False if not found
        """
        if task_id in self.tasks:
            del self.tasks[task_id]
            
            # Cancel if running
            if task_id in self._running_tasks:
                self._running_tasks[task_id].cancel()
                del self._running_tasks[task_id]
            
            logger.info(f"Deleted task: {task_id}")
            return True
        
        return False

    async def execute_task(
        self,
        task_id: str,
        execution_func,
    ) -> Optional[Task]:
        """Execute a task with a provided function.

        Args:
            task_id: Task ID
            execution_func: Async function to execute

        Returns:
            Updated task
        """
        task = self.tasks.get(task_id)
        if not task:
            logger.error(f"Task not found: {task_id}")
            return None
        
        # Check dependencies
        pending_deps = []
        for dep_id in task.dependencies:
            dep_task = self.tasks.get(dep_id)
            if not dep_task or dep_task.status != TaskStatus.COMPLETED:
                pending_deps.append(dep_id)
        
        if pending_deps:
            error_msg = f"Dependencies not completed: {', '.join(pending_deps)}"
            await self.update_task_status(task_id, TaskStatus.PENDING, error=error_msg)
            logger.warning(f"Task {task_id} waiting for dependencies")
            return task
        
        # Update to in-progress
        await self.update_task_status(task_id, TaskStatus.IN_PROGRESS, progress=0.0)
        
        # Create async task for execution
        async def _execute_wrapper():
            try:
                # Execute the function
                result = await execution_func(task)
                
                # Mark as completed
                await self.update_task_status(
                    task_id,
                    TaskStatus.COMPLETED,
                    progress=100.0,
                    result={"data": result} if result else {},
                )
                
                logger.info(f"Task {task_id} completed successfully")
                
            except asyncio.CancelledError:
                await self.update_task_status(task_id, TaskStatus.CANCELLED)
                logger.info(f"Task {task_id} cancelled")
                
            except Exception as e:
                error_msg = str(e)
                await self.update_task_status(task_id, TaskStatus.FAILED, error=error_msg)
                logger.error(f"Task {task_id} failed: {error_msg}")
        
        # Start execution
        self._running_tasks[task_id] = asyncio.create_task(_execute_wrapper())
        
        return task

    async def get_task_statistics(self) -> Dict[str, Any]:
        """Get task statistics.

        Returns:
            Dictionary with task statistics
        """
        total_tasks = len(self.tasks)
        
        status_counts = {}
        for status in TaskStatus:
            status_counts[status.value] = sum(
                1 for t in self.tasks.values() if t.status == status
            )
        
        priority_counts = {}
        for priority in TaskPriority:
            priority_counts[priority.value] = sum(
                1 for t in self.tasks.values() if t.priority == priority
            )
        
        avg_progress = 0.0
        if total_tasks > 0:
            avg_progress = sum(t.progress for t in self.tasks.values()) / total_tasks
        
        return {
            "total_tasks": total_tasks,
            "status_counts": status_counts,
            "priority_counts": priority_counts,
            "average_progress": round(avg_progress, 2),
            "running_tasks": len(self._running_tasks),
        }

    def _generate_task_id(self, name: str) -> str:
        """Generate a unique task ID.

        Args:
            name: Task name

        Returns:
            Unique task ID
        """
        import hashlib
        timestamp = datetime.now().isoformat()
        combined = f"{name}_{timestamp}"
        return hashlib.sha256(combined.encode()).hexdigest()[:16]
