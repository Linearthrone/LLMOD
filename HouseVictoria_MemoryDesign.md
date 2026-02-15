## Unified Memory System – Design Proposal

### Goals & scope
- Serve all surfaces: HouseVictoria App (WPF), backend Services, Core domain, and MCP server.
- Memory types: conversational/episodic, semantic/vector, task/workflow state, app/system config + caches, long‑term knowledge/notes, logs/observability crumbs.
- Tenancy: user, persona/agent, project/workspace, system/global; support lineage tags (who/what/when).
- Qualities: low-latency retrieval, durable where needed, graceful degradation offline, pluggable storage, privacy-first, observable, and summarization/compaction to stay “alive” without bloat.

### Architecture
- **Layered service**  
  - API surface in Core (`IMemoryService`, `ISemanticMemoryService`, `IMemorySummarizer`, `IMemoryRetentionPolicy`).  
  - Broker/Orchestrator in Services to route to backends and enforce policy/quota.  
  - Client adapters for App (WPF), MCP server, and other services.
- **Backends (pluggable)**  
  - Structured store: SQLite (default local), Postgres (shared/remote) for durability and indexing.  
  - Vector store: FAISS/SQLite-FTS fallback; optional Postgres pgvector or cloud vector DB.  
  - Cache: in-memory LRU for hot items.  
  - Files: optional blob storage for large artifacts, referenced by memory metadata.
- **Pipelines**  
  - Ingestion: normalize → embed (if text) → store (KV + vector) → index → emit events.  
  - Retrieval: lexical (FTS) + semantic (vector) + filters (tenant/type/tags/time/importance); rank + dedupe + constrain tokens.  
  - Summarization/rollup: periodic/background; replace verbose histories with summaries + pointers.  
  - GC/retention: TTL/LRU/importance decay; pinned items bypass GC.
- **Security**: encrypt at rest (DB/file-level), TLS in transit (service/MCP), ACL by tenant/persona/project, audit events.

### Data model (contract)
- **MemoryItem**: `id`, `tenantId`, `personaId`, `projectId`, `type` (conversation|task|note|config|log|artifact), `content` (text/json/blob-ref), `embedding` (vector), `metadata` (tags, source, channel, callId, cost, model), `importance` (0-1), `ttl`, `pinned`, `createdAt`, `updatedAt`, `lastAccessed`, `lineage` (who/what trigger), `checksum`.
- **ConversationTurn**: `sessionId`, `role`, `content`, `metadata`, `timestamp`, `embeddingRef`, `tokenCount`.
- **TaskState**: `taskId`, `status`, `inputs`, `outputs`, `toolCalls`, `messages`, `metrics`, `lineage`.
- **Indexes**: FTS on content/metadata, vector index on embeddings, composite on (tenant, persona, project, type, createdAt).
- **Suggested Core interfaces** (add in `HouseVictoria.Core/Interfaces`): `IMemoryService` (upsert/get/delete/search), `ISemanticMemoryService` (embed+search), `IMemoryRetentionPolicy` (decide expiration), `IMemorySummarizer` (summarize/rollup), `IMemoryEventSink` (log/audit).

### APIs & flows
- **Core ops**: `UpsertMemory(items)`, `GetMemory(ids)`, `SearchMemory(query, filters, limit)`, `SearchVector(embedding, filters, k)`, `DeleteMemory(ids|filters)`, `Pin/Unpin`, `Touch(id)` (access update).
- **Episodic flow**: append conversation turns → periodic summarize (by tokens/time) → store summary + anchor refs → prune old turns unless pinned/important.
- **Semantic flow**: embed text → store vector → hybrid search (lexical + vector) → rerank by recency/importance/tenant match → cap tokens for model context.
- **Task/workflow flow**: log tool calls, results, state transitions with lineage; expose retrieval by taskId/project/persona.
- **Event hooks**: before LLM call (fetch top-k memories), after LLM response (store message & summary candidate), on errors (log diagnostic memory), on tool completion (persist outputs).
- **Batching/streaming**: batch embeddings for throughput; stream long retrievals with early-yield pages; chunk long documents before embedding.
- **Suggested endpoints/adapters**: WPF app uses service client; MCP exposes tools; services expose gRPC/HTTP for memory ops (future).

### MCP integration
- Expose MCP tools/resources: `memory.search`, `memory.append`, `memory.summarize`, `memory.taskLog`.
- MCP tasks write with lineage: `taskId`, `toolName`, `agent`, `persona`, `project`, `runId`.
- Shared schemas with Core interfaces so MCP server and Services stay consistent; reuse vector + FTS backends where possible.
- Optionally mirror selective memories from app/services into MCP store (and vice versa) via sync jobs with filters (tenant/project/type).

### Policies & ops
- Retention: TTL per type; LRU with importance decay; pin to bypass; max items/tenant/persona; max tokens per session before summarize.
- Background jobs: summarize/rollup, compaction, expired/low-importance GC, embedding backfill.
- Observability: metrics (QPS, latency, hit/miss, cache hit, GC counts, summarize counts, embedding latency), logs with correlation IDs, traces around memory calls.
- Health: readiness (DB/vector reachable), liveness (loop alive), storage thresholds (warn on disk/full), integrity checks (checksum/row count).
- Backup/restore: snapshot SQLite/PG; export/import JSONL for portability; versioned schema migrations.

### Security & privacy
- ACL: enforce tenant/persona/project on every query; role-based access for system ops.
- Encryption: at rest (DB/file), in transit (HTTPS/TLS); redact secrets/PII in logs; optional field-level encryption for sensitive metadata.
- Audit: append-only audit events for memory writes/deletes/summaries; include requester, persona, project, tool, timestamps.
- Data minimization: drop transient logs quickly; configurable TTL defaults; allow “forget me” purge by tenant/persona.

### Migration path
- Phase 1: Define interfaces in Core; add Service broker stub that delegates to current storage (SQLite/FTS) and MCP memory for parity.
- Phase 2: Add vector backend option (pgvector/FAISS) behind config flag; keep SQLite FTS as fallback.
- Phase 3: Introduce summarizer/retention workers and pinning; wire conversation/task logging to new interfaces; add lineage tags.
- Phase 4: Expose MCP tools that call the shared broker; add quotas/metrics/health; roll out encryption at rest.
- Phase 5: Tune policies; enable backup/export; iterate on observability and redaction defaults.
