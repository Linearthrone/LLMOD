"""SQLite-based memory storage implementation."""

import aiosqlite
import json
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional

from ..config import get_config
from ..logger import get_logger

logger = get_logger("memory.storage")


class MemoryStorage:
    """Persistent memory storage using SQLite."""

    def __init__(self, db_path: Optional[str] = None):
        """Initialize memory storage.

        Args:
            db_path: Path to SQLite database file.
        """
        config = get_config()
        self.db_path = Path(db_path or config.database_path)
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        
    async def initialize(self) -> None:
        """Initialize database schema."""
        async with aiosqlite.connect(self.db_path) as db:
            await db.executescript("""
                -- Memory entries table
                CREATE TABLE IF NOT EXISTS memory_entries (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    key TEXT NOT NULL UNIQUE,
                    value TEXT NOT NULL,
                    metadata TEXT,
                    category TEXT,
                    importance REAL DEFAULT 1.0,
                    access_count INTEGER DEFAULT 0,
                    last_accessed TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                -- Conversation history table
                CREATE TABLE IF NOT EXISTS conversation_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL,
                    role TEXT NOT NULL,
                    content TEXT NOT NULL,
                    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    metadata TEXT
                );

                -- Knowledge graph table
                CREATE TABLE IF NOT EXISTS knowledge_graph (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    source_node TEXT NOT NULL,
                    target_node TEXT NOT NULL,
                    relation TEXT NOT NULL,
                    weight REAL DEFAULT 1.0,
                    metadata TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                -- Tags table for categorization
                CREATE TABLE IF NOT EXISTS tags (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE,
                    category TEXT,
                    description TEXT
                );

                -- Memory-tags relationship table
                CREATE TABLE IF NOT EXISTS memory_tags (
                    memory_id INTEGER NOT NULL,
                    tag_id INTEGER NOT NULL,
                    PRIMARY KEY (memory_id, tag_id),
                    FOREIGN KEY (memory_id) REFERENCES memory_entries(id) ON DELETE CASCADE,
                    FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
                );

                -- Create indexes for performance
                CREATE INDEX IF NOT EXISTS idx_memory_key ON memory_entries(key);
                CREATE INDEX IF NOT EXISTS idx_memory_category ON memory_entries(category);
                CREATE INDEX IF NOT EXISTS idx_memory_importance ON memory_entries(importance);
                CREATE INDEX IF NOT EXISTS idx_memory_updated ON memory_entries(updated_at);
                CREATE INDEX IF NOT EXISTS idx_conversation_session ON conversation_history(session_id);
                CREATE INDEX IF NOT EXISTS idx_conversation_timestamp ON conversation_history(timestamp);
                CREATE INDEX IF NOT EXISTS idx_graph_source ON knowledge_graph(source_node);
                CREATE INDEX IF NOT EXISTS idx_graph_target ON knowledge_graph(target_node);
                
                -- Full-text search index
                CREATE VIRTUAL TABLE IF NOT EXISTS memory_fts USING fts5(
                    key, value, metadata, content='memory_entries', content_rowid='id'
                );
                
                -- Triggers to keep FTS in sync
                CREATE TRIGGER IF NOT EXISTS memory_fts_insert AFTER INSERT ON memory_entries BEGIN
                    INSERT INTO memory_fts(rowid, key, value, metadata)
                    VALUES (NEW.id, NEW.key, NEW.value, NEW.metadata);
                END;
                
                CREATE TRIGGER IF NOT EXISTS memory_fts_delete AFTER DELETE ON memory_entries BEGIN
                    DELETE FROM memory_fts WHERE rowid = OLD.id;
                END;
                
                CREATE TRIGGER IF NOT EXISTS memory_fts_update AFTER UPDATE ON memory_entries BEGIN
                    UPDATE memory_fts SET key = NEW.key, value = NEW.value, metadata = NEW.metadata
                    WHERE rowid = NEW.id;
                END;
            """)
            await db.commit()
            logger.info(f"Memory storage initialized at {self.db_path}")

    async def store(
        self,
        key: str,
        value: Any,
        category: Optional[str] = None,
        importance: float = 1.0,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> int:
        """Store a value in memory.

        Args:
            key: Unique key for the memory.
            value: Value to store (will be JSON serialized).
            category: Optional category.
            importance: Importance score (0-1).
            metadata: Optional metadata dictionary.

        Returns:
            Memory entry ID.
        """
        value_str = json.dumps(value) if not isinstance(value, str) else value
        metadata_str = json.dumps(metadata) if metadata else None
        
        async with aiosqlite.connect(self.db_path) as db:
            cursor = await db.execute("""
                INSERT OR REPLACE INTO memory_entries 
                (key, value, metadata, category, importance, updated_at)
                VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
            """, (key, value_str, metadata_str, category, importance))
            await db.commit()
            memory_id = cursor.lastrowid
            logger.debug(f"Stored memory: {key} (ID: {memory_id})")
            return memory_id

    async def retrieve(self, key: str) -> Optional[Dict[str, Any]]:
        """Retrieve a value from memory.

        Args:
            key: Memory key.

        Returns:
            Dictionary with memory entry or None if not found.
        """
        async with aiosqlite.connect(self.db_path) as db:
            db.row_factory = aiosqlite.Row
            async with db.execute("""
                SELECT * FROM memory_entries WHERE key = ?
            """, (key,)) as cursor:
                row = await cursor.fetchone()
                
                if row:
                    # Update access count and last accessed
                    await db.execute("""
                        UPDATE memory_entries 
                        SET access_count = access_count + 1,
                            last_accessed = CURRENT_TIMESTAMP
                        WHERE key = ?
                    """, (key,))
                    await db.commit()
                    
                    metadata = json.loads(row["metadata"]) if row["metadata"] else None
                    value = row["value"]
                    
                    # Try to parse as JSON, otherwise return as string
                    try:
                        value = json.loads(value)
                    except (json.JSONDecodeError, TypeError):
                        pass
                    
                    return {
                        "id": row["id"],
                        "key": row["key"],
                        "value": value,
                        "metadata": metadata,
                        "category": row["category"],
                        "importance": row["importance"],
                        "access_count": row["access_count"],
                        "last_accessed": row["last_accessed"],
                        "created_at": row["created_at"],
                        "updated_at": row["updated_at"],
                    }
        
        return None

    async def search(
        self,
        query: str,
        category: Optional[str] = None,
        limit: int = 10,
    ) -> List[Dict[str, Any]]:
        """Search memory using full-text search.

        Args:
            query: Search query.
            category: Optional category filter.
            limit: Maximum results.

        Returns:
            List of matching memory entries.
        """
        async with aiosqlite.connect(self.db_path) as db:
            db.row_factory = aiosqlite.Row
            
            if category:
                async with db.execute("""
                    SELECT m.* FROM memory_entries m
                    INNER JOIN memory_fts fts ON m.id = fts.rowid
                    WHERE memory_fts MATCH ? AND m.category = ?
                    ORDER BY m.importance DESC, m.updated_at DESC
                    LIMIT ?
                """, (query, category, limit)) as cursor:
                    rows = await cursor.fetchall()
            else:
                async with db.execute("""
                    SELECT m.* FROM memory_entries m
                    INNER JOIN memory_fts fts ON m.id = fts.rowid
                    WHERE memory_fts MATCH ?
                    ORDER BY m.importance DESC, m.updated_at DESC
                    LIMIT ?
                """, (query, limit)) as cursor:
                    rows = await cursor.fetchall()
            
            results = []
            for row in rows:
                metadata = json.loads(row["metadata"]) if row["metadata"] else None
                value = row["value"]
                try:
                    value = json.loads(value)
                except (json.JSONDecodeError, TypeError):
                    pass
                
                results.append({
                    "id": row["id"],
                    "key": row["key"],
                    "value": value,
                    "metadata": metadata,
                    "category": row["category"],
                    "importance": row["importance"],
                })
            
            return results
