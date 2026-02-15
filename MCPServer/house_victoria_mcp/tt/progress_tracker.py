"""Progress tracking for TT (Task & Workflow) functionality."""

from datetime import datetime, timedelta
from enum import Enum
from typing import Any, Dict, List, Optional
from dataclasses import dataclass, field

from ..logger import get_logger

logger = get_logger("tt.progress_tracker")


class ProgressState(str, Enum):
    """Progress state enumeration."""
    NOT_STARTED = "not_started"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    FAILED = "failed"
    PAUSED = "paused"


@dataclass
class ProgressUpdate:
    """Progress update data structure."""
    
    id: str
    entity_type: str  # "task" or "workflow"
    entity_id: str
    progress: float  # 0.0 to 100.0
    state: ProgressState = ProgressState.NOT_STARTED
    message: Optional[str] = None
    data: Dict[str, Any] = field(default_factory=dict)
    timestamp: datetime = field(default_factory=datetime.now)


@dataclass
class ProgressMeter:
    """Progress meter for tracking long-running operations."""
    
    id: str
    name: str
    total_steps: int
    current_step: int = 0
    progress: float = 0.0
    state: ProgressState = ProgressState.NOT_STARTED
    started_at: Optional[datetime] = None
    estimated_completion: Optional[datetime] = None
    updates: List[ProgressUpdate] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)

    @property
    def time_elapsed(self) -> Optional[timedelta]:
        """Get time elapsed since start."""
        if self.started_at:
            return datetime.now() - self.started_at
        return None

    @property
    def is_complete(self) -> bool:
        """Check if progress is complete."""
        return self.state == ProgressState.COMPLETED or self.current_step >= self.total_steps


class ProgressTracker:
    """Manager for tracking progress of tasks and workflows."""

    def __init__(self):
        """Initialize progress tracker."""
        self.meters: Dict[str, ProgressMeter] = {}
        self._callbacks: List[callable] = []

    def register_callback(self, callback: callable) -> None:
        """Register a callback for progress updates.

        Args:
            callback: Function to call on progress updates
        """
        self._callbacks.append(callback)
        logger.debug(f"Registered progress callback: {callback.__name__}")

    async def create_meter(
        self,
        name: str,
        total_steps: int,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> ProgressMeter:
        """Create a new progress meter.

        Args:
            name: Meter name
            total_steps: Total number of steps
            metadata: Additional metadata

        Returns:
            Created progress meter
        """
        meter_id = self._generate_meter_id(name)
        
        meter = ProgressMeter(
            id=meter_id,
            name=name,
            total_steps=total_steps,
            metadata=metadata or {},
        )
        
        self.meters[meter_id] = meter
        logger.info(f"Created progress meter: {meter_id} - {name}")
        
        return meter

    async def update_progress(
        self,
        meter_id: str,
        current_step: Optional[int] = None,
        progress: Optional[float] = None,
        state: Optional[ProgressState] = None,
        message: Optional[str] = None,
        data: Optional[Dict[str, Any]] = None,
    ) -> Optional[ProgressMeter]:
        """Update progress for a meter.

        Args:
            meter_id: Meter ID
            current_step: Current step number
            progress: Progress percentage (0-100)
            state: Progress state
            message: Status message
            data: Additional data

        Returns:
            Updated meter or None if not found
        """
        meter = self.meters.get(meter_id)
        if not meter:
            return None
        
        # Update state
        if state is not None:
            meter.state = state
            
            # Start/complete tracking based on state
            if state == ProgressState.IN_PROGRESS and meter.started_at is None:
                meter.started_at = datetime.now()
            elif state == ProgressState.COMPLETED:
                meter.current_step = meter.total_steps
                meter.progress = 100.0
        
        # Update step count
        if current_step is not None:
            meter.current_step = current_step
            meter.progress = (current_step / meter.total_steps) * 100.0
        
        # Update progress directly
        if progress is not None:
            meter.progress = max(0.0, min(100.0, progress))
            meter.current_step = int((meter.progress / 100.0) * meter.total_steps)
        
        # Update message
        if message is not None:
            # Create progress update
            update = ProgressUpdate(
                id=self._generate_update_id(meter_id),
                entity_type="meter",
                entity_id=meter_id,
                progress=meter.progress,
                state=meter.state,
                message=message,
                data=data or {},
            )
            
            meter.updates.append(update)
            
            # Notify callbacks
            for callback in self._callbacks:
                try:
                    await callback(update)
                except Exception as e:
                    logger.error(f"Progress callback error: {str(e)}")
        
        # Estimate completion time
        if meter.started_at and meter.state == ProgressState.IN_PROGRESS:
            elapsed = (datetime.now() - meter.started_at).total_seconds()
            if meter.progress > 0:
                time_per_progress = elapsed / meter.progress
                remaining_progress = 100.0 - meter.progress
                remaining_seconds = time_per_progress * remaining_progress
                meter.estimated_completion = datetime.now() + timedelta(seconds=remaining_seconds)
        
        logger.debug(f"Updated progress: {meter_id} - {meter.progress:.1f}%")
        return meter

    async def get_meter(self, meter_id: str) -> Optional[ProgressMeter]:
        """Get a progress meter.

        Args:
            meter_id: Meter ID

        Returns:
            Progress meter or None if not found
        """
        return self.meters.get(meter_id)

    async def list_meters(
        self,
        state: Optional[ProgressState] = None,
    ) -> List[ProgressMeter]:
        """List progress meters with optional filter.

        Args:
            state: Filter by state

        Returns:
            Filtered list of meters
        """
        meters = list(self.meters.values())
        
        if state:
            meters = [m for m in meters if m.state == state]
        
        return meters

    async def delete_meter(self, meter_id: str) -> bool:
        """Delete a progress meter.

        Args:
            meter_id: Meter ID

        Returns:
            True if deleted, False if not found
        """
        if meter_id in self.meters:
            del self.meters[meter_id]
            logger.info(f"Deleted progress meter: {meter_id}")
            return True
        return False

    async def get_meter_summary(self, meter_id: str) -> Optional[Dict[str, Any]]:
        """Get a summary of a progress meter.

        Args:
            meter_id: Meter ID

        Returns:
            Summary dictionary or None
        """
        meter = self.meters.get(meter_id)
        if not meter:
            return None
        
        return {
            "id": meter.id,
            "name": meter.name,
            "progress": round(meter.progress, 2),
            "state": meter.state.value,
            "current_step": meter.current_step,
            "total_steps": meter.total_steps,
            "started_at": meter.started_at.isoformat() if meter.started_at else None,
            "estimated_completion": meter.estimated_completion.isoformat() if meter.estimated_completion else None,
            "time_elapsed": str(meter.time_elapsed) if meter.time_elapsed else None,
            "is_complete": meter.is_complete,
            "updates_count": len(meter.updates),
            "metadata": meter.metadata,
        }

    def _generate_meter_id(self, name: str) -> str:
        """Generate a unique meter ID.

        Args:
            name: Meter name

        Returns:
            Unique meter ID
        """
        import hashlib
        timestamp = datetime.now().isoformat()
        combined = f"meter:{name}:{timestamp}"
        return hashlib.sha256(combined.encode()).hexdigest()[:16]

    def _generate_update_id(self, meter_id: str) -> str:
        """Generate a unique update ID.

        Args:
            meter_id: Meter ID

        Returns:
            Unique update ID
        """
        import hashlib
        timestamp = datetime.now().isoformat()
        combined = f"update:{meter_id}:{timestamp}"
        return hashlib.sha256(combined.encode()).hexdigest()[:8]
