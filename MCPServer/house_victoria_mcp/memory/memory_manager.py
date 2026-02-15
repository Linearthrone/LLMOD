"""Memory manager for managing persistent memory operations."""

import aiosqlite
import hashlib
import json
from typing import Any, Dict, List, Optional
from datetime import datetime

from .storage import MemoryStorage
from ..logger import get_logger

logger = get_logger("memory.manager")


class MemoryManager:
    """Manager for persistent memory operations."""

    def __init__(self, storage: Optional[MemoryStorage] = None):
        """Initialize memory manager.

        Args:
            storage: Memory storage instance. If None, creates default.
        """
        self.storage = storage or MemoryStorage()

    async def initialize(self) -> None:
        """Initialize the memory system."""
        await self.storage.initialize()
        logger.info("Memory manager initialized")

    def generate_key(self, value: Any) -> str:
        """Generate a unique key from a value.

        Args:
            value: Value to generate key from.

        Returns:
            SHA256 hash of the value.
        """
        value_str = str(value)
        return hashlib.sha256(value_str.encode()).hexdigest()[:32]

    async def remember(
        self,
        value: Any,
        key: Optional[str] = None,
        metadata: Optional[Dict[str, Any]] = None,
        category: Optional[str] = None,
        importance: float = 1.0,
    ) -> str:
        """Remember information in persistent memory.

        Args:
            value: Value to remember.
            key: Optional custom key. If not provided, generates one.
            metadata: Additional metadata.
            category: Category for organization.
            importance: Importance score (0-1).

        Returns:
            The key used to store the memory.
        """
        if key is None:
            key = self.generate_key(value)

        memory_id = await self.storage.store(
            key=key,
            value=value,
            metadata=metadata,
            category=category,
            importance=importance,
        )

        logger.info(f"Remembered: {key[:32]}... (ID: {memory_id})")
        return key

    async def recall(self, key: str) -> Optional[Any]:
        """Recall information from memory.

        Args:
            key: Memory key to recall.

        Returns:
            The stored value or None if not found.
        """
        entry = await self.storage.retrieve(key)
        if entry:
            logger.debug(f"Recalled: {key[:32]}...")
            return entry["value"]
        
        logger.warning(f"Memory not found: {key[:32]}...")
        return None

    async def search_memory(
        self,
        query: str,
        category: Optional[str] = None,
        limit: int = 10,
    ) -> List[Dict[str, Any]]:
        """Search memory for information.

        Args:
            query: Search query string.
            category: Optional category filter.
            limit: Maximum number of results.

        Returns:
            List of matching memory entries.
        """
        results = await self.storage.search(query, category=category, limit=limit)
        logger.debug(f"Search '{query}' returned {len(results)} results")
        return results

    async def get_memory_stats(self) -> Dict[str, Any]:
        """Get statistics about the memory system.

        Returns:
            Dictionary with memory statistics.
        """
        stats = {
            "total_entries": 0,
            "by_category": {},
            "total_access_count": 0,
            "average_importance": 0.0,
        }
        
        async with aiosqlite.connect(self.storage.db_path) as db:
            # Total entries
            async with db.execute("SELECT COUNT(*) FROM memory_entries") as cursor:
                row = await cursor.fetchone()
                stats["total_entries"] = row[0] if row else 0
            
            # By category
            async with db.execute("""
                SELECT category, COUNT(*) as count 
                FROM memory_entries 
                WHERE category IS NOT NULL
                GROUP BY category
            """) as cursor:
                async for row in cursor:
                    stats["by_category"][row[0]] = row[1]
            
            # Total access count
            async with db.execute("SELECT SUM(access_count) FROM memory_entries") as cursor:
                row = await cursor.fetchone()
                stats["total_access_count"] = row[0] if row and row[0] else 0
            
            # Average importance
            async with db.execute("SELECT AVG(importance) FROM memory_entries") as cursor:
                row = await cursor.fetchone()
                stats["average_importance"] = float(row[0]) if row and row[0] else 0.0
        
        return stats

    async def remember_conversation(
        self,
        session_id: str,
        role: str,
        content: str,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> None:
        """Remember a conversation message.

        Args:
            session_id: Unique session identifier.
            role: Message role (user, assistant, system).
            content: Message content.
            metadata: Additional metadata.
        """
        metadata_str = json.dumps(metadata) if metadata else None
        
        async with aiosqlite.connect(self.storage.db_path) as db:
            await db.execute("""
                INSERT INTO conversation_history 
                (session_id, role, content, metadata)
                VALUES (?, ?, ?, ?)
            """, (session_id, role, content, metadata_str))
            await db.commit()
        
        logger.debug(f"Logged conversation: {session_id}/{role}")

    async def recall_conversation(
        self,
        session_id: str,
        limit: int = 50,
    ) -> List[Dict[str, Any]]:
        """Recall conversation history for a session.

        Args:
            session_id: Unique session identifier.
            limit: Maximum number of messages.

        Returns:
            List of conversation messages in chronological order.
        """
        messages = []
        
        async with aiosqlite.connect(self.storage.db_path) as db:
            db.row_factory = aiosqlite.Row
            async with db.execute("""
                SELECT * FROM conversation_history
                WHERE session_id = ?
                ORDER BY timestamp ASC
                LIMIT ?
            """, (session_id, limit)) as cursor:
                async for row in cursor:
                    metadata = json.loads(row["metadata"]) if row["metadata"] else None
                    messages.append({
                        "session_id": row["session_id"],
                        "role": row["role"],
                        "content": row["content"],
                        "timestamp": row["timestamp"],
                        "metadata": metadata,
                    })
        
        logger.debug(f"Recalled {len(messages)} messages for session {session_id}")
        return messages
