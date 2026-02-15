# Concurrent Priority Web Crawler in C# (.NET)

This repository contains a from-scratch, modular, concurrent web crawler implemented in C# using .NET.

The goal of this project is to demonstrate:

- Correct concurrent architecture
- Thread-safe coordination
- Priority-based crawl scheduling
- Domain-aware rate limiting
- Clean separation of concerns
- Production-grade async design patterns

This is not a toy crawler. It is a structured, scalable foundation for understanding real crawler systems.

---

# 1. High-Level Overview

At a high level, the crawler follows this recursive flow:

URL → Fetch → Parse → (Extract Data + Links) → Re-enqueue → Repeat


Multiple workers operate concurrently while sharing a synchronized URL frontier.

The system ensures:
- No duplicate crawling
- Controlled parallelism
- Per-domain politeness
- Graceful termination

---

# 2. Core Architecture

## 2.1 URL Frontier

The URL Frontier is the central scheduling mechanism.

Responsibilities:
- Store URLs waiting to be processed
- Prevent duplicate insertions
- Maintain crawl priority
- Block workers efficiently when empty

Internally uses:
- `PriorityQueue<FrontierItem>`
- `ConcurrentDictionary<string, bool>` for visited tracking
- `SemaphoreSlim` for blocking coordination

### Priority Model

Not all URLs are equal. The crawler assigns priority using a scoring heuristic:

score = [(maxDepth - currentDepth) * 10]  [domainBoost, keywordBoost, penalties(.pdf, .zip)]

