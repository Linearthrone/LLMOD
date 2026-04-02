"""Vector search backed by the same Postgres + pgvector table as the House Victoria app."""

from __future__ import annotations

import json
import os
from typing import Any, Dict, List, Optional

import httpx

from ..logger import get_logger

logger = get_logger("memory.vector_search")

# Same table as HouseVictoria.Services.Memory.PgVectorClient
_TABLE = "house_victoria_memory_embeddings"


async def _ollama_embed(text: str, base_url: str, model: str) -> List[float]:
    base = base_url.rstrip("/")
    url_embed = f"{base}/api/embed"
    url_legacy = f"{base}/api/embeddings"
    body_new = {"model": model, "input": text}
    async with httpx.AsyncClient(timeout=120.0) as client:
        r = await client.post(url_embed, json=body_new)
        if r.status_code >= 400:
            r = await client.post(url_legacy, json={"model": model, "prompt": text})
        r.raise_for_status()
        data = r.json()
        if "embeddings" in data and data["embeddings"]:
            vec = data["embeddings"][0]
        elif "embedding" in data:
            vec = data["embedding"]
        else:
            raise RuntimeError("Unexpected Ollama embed response")
        return [float(x) for x in vec]


def _format_vector_literal(vec: List[float]) -> str:
    return "[" + ",".join(f"{v:.17g}" for v in vec) + "]"


class VectorSearch:
    """Vector-based semantic search using Postgres pgvector (shared with the WPF app)."""

    def __init__(self) -> None:
        self._pg_dsn = os.environ.get("PGVECTOR_CONNECTION_STRING", "").strip()
        self._ollama = os.environ.get("OLLAMA_HOST", "http://127.0.0.1:11434").strip()
        self._embed_model = os.environ.get("OLLAMA_EMBEDDING_MODEL", "nomic-embed-text").strip()
        logger.info(
            "VectorSearch: pgvector=%s ollama=%s model=%s",
            "on" if self._pg_dsn else "off",
            self._ollama,
            self._embed_model,
        )

    async def index(self, key: str, value: Any, metadata: Optional[Dict[str, Any]] = None) -> None:
        """Optional: index is normally driven by the WPF app when saving memory."""
        logger.debug("index() called for %s (use app upsert for full parity)", key[:32])

    async def search(
        self,
        query: str,
        limit: int = 10,
    ) -> List[Dict[str, Any]]:
        if not self._pg_dsn:
            logger.warning("PGVECTOR_CONNECTION_STRING not set; vector search returns [].")
            return []

        try:
            import asyncpg
        except ImportError:
            logger.error("asyncpg not installed; pip install asyncpg")
            return []

        try:
            vec = await _ollama_embed(query or "", self._ollama, self._embed_model)
        except Exception as ex:
            logger.exception("Ollama embed failed: %s", ex)
            return []

        literal = _format_vector_literal(vec)
        sql = f"""
SELECT id, content, 1 - (embedding <=> $1::vector) AS score
FROM {_TABLE}
ORDER BY embedding <=> $1::vector
LIMIT $2
"""
        try:
            conn = await asyncpg.connect(self._pg_dsn)
            try:
                rows = await conn.fetch(sql, literal, limit)
            finally:
                await conn.close()
        except Exception as ex:
            logger.exception("pgvector query failed: %s", ex)
            return []

        out: List[Dict[str, Any]] = []
        for r in rows:
            out.append(
                {
                    "id": r["id"],
                    "content": r["content"],
                    "score": float(r["score"]),
                }
            )
        return out
