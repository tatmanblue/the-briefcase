# Briefcase Search — Design Proposal

## Context

The current `list_files` tool returns file metadata (ID, name, size, last modified) but gives agents no way to find files by content or by partial name. This proposal evaluates approaches for a `search_files` MCP tool and recommends a path forward.

---

## What Agents Actually Need

An agent searching the Briefcase will typically be doing one of:

1. **Name lookup** — "Find the file called something like 'quarterly-report'"
2. **Content lookup** — "Find files that mention 'authentication' or 'OAuth'"
3. **Topic lookup** — "Find anything related to the onboarding process" (semantic / fuzzy)

Each of these maps to a different implementation tier, with increasing complexity and storage cost.

---

## Options

### Option A — On-Demand Search (No Index)

Search is performed at query time by scanning the registry and, for content search, reading file contents directly.

**How it works:**
- Filename search: in-memory string matching against `RegistryEntry.Name` — instant.
- Content search: iterate registered files, read each as text, scan for the query string. Binary files and files over a configurable size limit are skipped.

**Pros:**
- Zero additional storage — no new files beyond `registry.json`.
- Zero maintenance — no index to keep synchronized.
- Simple to implement (~100 lines).
- Correct by definition: always reads the current file contents.

**Cons:**
- Content search speed is proportional to total size of all registered text files.
- Repeated searches re-read the same files.
- Practical upper bound: comfortable up to ~300 text files / ~30 MB total before latency becomes noticeable (rough estimate; hardware-dependent).

---

### Option B — Cached Word Sets (Lightweight Index)

Extend Option A with a lazy-built, per-file word-set cache stored in `search-cache.json` alongside `registry.json`.

**How it works:**
- On first content search (or on startup if configured), extract the set of unique lowercase words from each text file and store: `{ fileId, lastModifiedUtc, words: [...] }`.
- On subsequent searches, use the cached word set instead of re-reading the file.
- FileWatcher change events mark a cache entry as dirty; it is rebuilt on next search or proactively in the background.
- Binary files and oversized files are excluded (same as Option A).

**Storage estimate:** roughly 2–8 KB per file (word set only, no positions or frequencies). A 500-file collection would produce a ~1–4 MB cache file — modest.

**Pros:**
- Significantly faster repeated searches on large collections.
- Self-healing: stale entries are detected via `lastModifiedUtc` comparison and rebuilt automatically.
- Still uses `BRIEFCASE_DATA_PATH` — no new infrastructure.

**Cons:**
- Adds a new `search-cache.json` file that needs to stay consistent with `registry.json`.
- First search (cold cache) is as slow as Option A.
- Word-set matching is whole-word only (no substring within a word unless the query is itself a full word).
- More moving parts: cache invalidation, background rebuild logic, serialization.

---

### Option C — AI-Extracted Keywords / Summaries

During file registration or change events, call an LLM (e.g., via the Claude API) to generate a short keyword list and/or one-sentence summary for each file. Store these in the registry entry.

**Pros:**
- Semantic search capability — agents can find "files about invoicing" even if the word "invoice" does not appear literally.
- Small storage footprint per file (a few hundred characters of keywords/summary).

**Cons:**
- Requires an external API dependency (Claude API key, network access, latency, cost).
- Adds complexity around when to call the API (on add? on change? on demand?).
- Results are non-deterministic and may need human review for sensitive files.
- Violates the "don't overburden the Briefcase" constraint — the server would need to make LLM calls as a side effect of file events.

**Verdict:** Out of scope for V2. Worth revisiting only if agents consistently fail to find files via keyword search.

---

## Recommendation

**Implement Option A now; design the `search_files` tool interface to be forward-compatible with Option B.**

Option A covers the vast majority of real agent use cases with minimal complexity and zero storage cost. The interface should be designed so that adding Option B later is purely additive (a new env var to enable caching, no tool API changes).

---

## Proposed `search_files` Tool Interface

```
search_files(query, searchIn?, limit?, sort?)
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `query` | string | required | Substring to search for. Case-insensitive. |
| `searchIn` | string | `"both"` | `"name"` — filename only; `"content"` — file contents only; `"both"` — name first, then content |
| `limit` | int? | server default | Same semantics as `list_files`. Negative or omitted = use `BRIEFCASE_SEARCH_DEFAULT_LIMIT`. |
| `sort` | string? | `"modified_desc"` | Same options as `list_files`: `modified_desc`, `modified_asc`, `name_asc`, `name_desc`, `default`. |

**Response shape** (same as `list_files`, plus a `matchedIn` field):

```json
[
  {
    "id": "...",
    "name": "quarterly-report.md",
    "size": 4096,
    "lastModified": "2026-04-15T10:30:00Z",
    "matchedIn": "name"
  },
  {
    "id": "...",
    "name": "notes.txt",
    "size": 1200,
    "lastModified": "2026-04-20T08:00:00Z",
    "matchedIn": "content"
  }
]
```

`matchedIn` is `"name"`, `"content"`, or `"both"` — tells the agent why the file was returned.

---

## New Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `BRIEFCASE_SEARCH_DEFAULT_LIMIT` | No | `25` | Default max results for `search_files`. Negative = no limit. |
| `BRIEFCASE_SEARCH_MAX_FILE_SIZE_KB` | No | `512` | Files larger than this (in KB) are skipped during content search. Prevents the server from reading huge binaries. |

---

## Implementation Sketch (Option A)

**New files:**
- `src/Briefcase/Tools/SearchFilesTool.cs`

**No changes required to:**
- `FileRegistry` — `GetAll()` already provides everything needed.
- `AppSettings` — two new properties (`SearchDefaultLimit`, `SearchMaxFileSizeKb`).
- `Program.cs` — read two new env vars; register `SearchFilesTool`.

**Core logic:**

```
1. Get all entries from registry.
2. For each entry:
   a. Check name match (if searchIn is "name" or "both").
   b. If file is text and within size limit, check content match (if searchIn is "content" or "both").
   c. Skip binary files (detect via null bytes in first 512 bytes).
3. Collect matches with matchedIn metadata.
4. Sort and apply limit (same helpers as list_files).
5. Return serialized results.
```

Binary detection is cheap: read the first 512 bytes and check for null bytes. This reliably excludes executables, images, archives, and other non-text formats.

---

## Open Questions

Before implementation, answers to these would sharpen the design:

1. **Collection size** — roughly how many files does a typical Briefcase deployment contain? (Dozens? Hundreds? Thousands?) This determines whether Option A's query-time scan is acceptable long-term.

2. **File types** — are the files predominantly text (markdown, code, plain text, CSV) or a mix that includes binaries (PDFs, images, Word docs)? If PDFs are common, content search would miss them unless a PDF text-extraction library is added.

3. **Match semantics** — should `query = "auth"` match files containing the word "authentication"? (Substring matching within words.) Or only files containing the exact token "auth"? Substring matching is more useful but slightly slower.

4. **Relevance ranking** — is a flat match/no-match result sufficient, or should results be ranked by how many times the query appears in the file?

5. **Latency tolerance** — is a 1–3 second response acceptable for a content search across a large collection, or does it need to feel instant?
