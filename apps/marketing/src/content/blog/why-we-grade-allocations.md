---
title: Why CodeBench grades allocations
description: A correct answer that allocates on every call is a production problem waiting to happen. Here is how we measure it, and why it's a budget rather than a score.
pubDate: 2026-11-24
tags: [allocations, performance, grading]
---

Most practice sites stop at "the tests pass". In a service that handles a few thousand requests a second, the
next question matters just as much: **what did it cost?** Not only in time — in memory the garbage collector
has to clean up afterwards.

## The same answer, twice

Both of these count the words that are at least `minLength` characters long:

```csharp
public int CountLongWords(string[] words, int minLength) =>
    words.Where(w => w.Length >= minLength).ToList().Count;
```

```csharp
public int CountLongWords(string[] words, int minLength)
{
    var count = 0;
    foreach (var word in words)
    {
        if (word.Length >= minLength) count++;
    }
    return count;
}
```

Both are correct. The first allocates a closure, a delegate, an iterator and a list that grows as it fills — on
every call; the second allocates nothing.
On a single call nobody notices. On a hot path called millions of times, the first one keeps the garbage
collector busy and shows up as latency spikes you can't reproduce on your laptop.

## Budgets, not leaderboards

CodeBench measures the bytes your code allocates while each test runs and compares them with a **budget** you
can see before you start. Within budget is a pass; over budget is `Budget exceeded`, even if the answer is right.

We use budgets instead of a ranking on purpose:

- A budget tells you what "good enough" means for this problem, up front.
- It doesn't reward shaving the last nanosecond off something that was already fine.
- It's stable: the same code gets the same verdict tomorrow.

Time works the same way. Each test runs seven times and the median is compared with its time budget, so one
noisy run on a busy machine doesn't fail you.

## What this isn't

It isn't a ban on LINQ. Plenty of problems have budgets loose enough for it, and readable code is part of the
grade too. The point is to make the cost visible — then you decide whether it's worth paying.
