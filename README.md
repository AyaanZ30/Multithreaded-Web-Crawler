# Modular Web Crawler in C# (.NET)

This repository is a **from-scratch, educational implementation of a web crawler** using **C# and .NET**.

The goal is **not** to rush into code, but to:

* Understand *what a crawler is*
* Understand *how threading and concurrency work*
* Learn the *C# primitives involved*
* Build the crawler **incrementally and modularly**

We will deliberately separate **learning concepts** from **production crawler code**.

---

## 1. What is a Web Crawler?

A **web crawler** is a program that:

1. Starts from one or more URLs (seed URLs)
2. Downloads the HTML of those pages
3. Extracts useful information
4. Extracts new links
5. Repeats the process recursively

In simple terms:

```
URL → Fetch HTML → Parse → Extract Data + Links → Repeat
```

### Key properties of a crawler

* It is **I/O-bound** (waiting on network most of the time)
* It must avoid **revisiting the same URL**
* It must be **polite** (not overload servers)
* It must be **concurrent** to be efficient

---

## 2. Core Concepts of Crawling (Mental Model)

Every crawler — regardless of language — has these parts:

### 2.1 URL Frontier

The **URL frontier** is the list (or queue) of URLs waiting to be crawled.

Properties:

* Thread-safe
* Can grow dynamically
* Feeds worker threads

### 2.2 Visited Set

A set of URLs that have already been processed.

Purpose:

* Prevent infinite loops
* Prevent duplicate work

### 2.3 Workers

Workers are concurrent units that:

* Take a URL from the frontier
* Fetch the page
* Process it
* Add new URLs back to the frontier

### 2.4 Termination Condition

The crawler must know **when to stop**:

* Max pages reached
* Frontier empty
* Time limit exceeded

---

## 3. Why Multithreading / Concurrency is Needed

If a crawler fetched pages **one at a time**, it would be extremely slow:

```
Fetch → Wait → Fetch → Wait → Fetch → Wait
```

Instead, we want:

```
Fetch → Fetch → Fetch → Fetch
(waiting happens in parallel)
```

This is why crawlers use:

* Threads
* Tasks
* Async I/O

---

## 4. Concurrency in C# (Conceptual)

C# gives us **high-level concurrency primitives**.

### 4.1 Thread vs Task

* **Thread**: OS-level execution unit (heavyweight)
* **Task**: Logical unit of work (lightweight, preferred)

We will use **Tasks**, not raw threads.

---

### 4.2 Async / Await (CRITICAL)

Async code allows us to:

* Start an operation
* Yield control while waiting
* Resume when the result is ready

Example:

```csharp
async Task<string> FetchAsync()
{
    var html = await httpClient.GetStringAsync(url);
    return html;
}
```

This does **not block threads** while waiting on the network.

---

### 4.3 Thread-Safe Collections

When multiple workers run at the same time, shared data must be protected.

C# provides **built-in thread-safe collections**:

* `ConcurrentDictionary<TKey, TValue>`
* `ConcurrentQueue<T>`
* `BlockingCollection<T>`

We will rely on these instead of manual locks where possible.

---

## 5. Project Structure (Planned)

```
WebCrawler/
 ├── Program.cs          (entry point)
 ├── Basics/             (learning playground)
 │    ├── Threading/
 │    ├── AsyncAwait/
 │    ├── ConcurrentCollections/
 │    └── SimpleCrawlerLoop/
 ├── Core/               (crawler fundamentals)
 ├── Crawling/           (fetching, workers)
 ├── Processing/         (HTML parsing, cleaning)
 └── Infrastructure/     (logging, persistence)
```

The **Basics** folder exists so we can:

* Learn concepts in isolation
* Write small, throwaway examples
* Build intuition before real crawler code

---

## 6. What We Will Build (Step-by-Step)

### Phase 1 – Fundamentals (Basics folder)

* Single-thread vs multi-thread examples
* Task scheduling
* Blocking collections
* Producer–consumer pattern
* Simple fake crawler loop (no HTTP)

### Phase 2 – Core Crawler

* URL Frontier
* Visited tracking
* Worker lifecycle
* Termination logic

### Phase 3 – Real Crawling

* HTTP fetching
* HTML parsing
* URL extraction
* Data cleaning (remove URLs)

### Phase 4 – Improvements

* Rate limiting
* Domain scoping
* Persistence
* Pluggable processors

---

## 7. Important Rules for This Project

1. **Clarity > Cleverness**
2. **Correctness > Performance**
3. **Small steps, always runnable**
4. **No copy–paste magic**
5. **Understand before optimizing**

---

## 8. What Comes Next

Next, we will:

1. Create the **Basics** folder
2. Write a **very small multithreading demo**
3. Visually see how work is distributed

Only after that will we touch crawler logic.

---

> If you understand the Basics folder completely, you will understand **any crawler written in any language**.

---

**Next step:**
👉 Create the `Basics` folder, and we’ll start with a **simple Task-based worker demo**.
