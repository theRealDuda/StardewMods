﻿namespace Common.Helpers.PatternPatcher;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// </summary>
/// <typeparam name="TItem"></typeparam>
internal class PatternPatcher<TItem>
{
    public PatternPatcher(Func<TItem, TItem, bool> comparer)
    {
        this.Comparer = comparer;
    }

    public int TotalPatches { get; private set; }

    public int AppliedPatches { get; private set; }

    private Func<TItem, TItem, bool> Comparer { get; }

    private bool Done { get; set; }

    private PatternPatch CurrentPatch { get; set; }

    private Queue<PatternPatch> Patches { get; } = new();

    private IEnumerator<TItem> CodeEnum { get; set; }

    private IList<TItem> ItemBuffer { get; } = new List<TItem>();

    private PatternPatch LastPatch { get; set; }

    /// <summary>
    ///     Allows patching a list in place after a specific pattern block is matched.
    /// </summary>
    /// <param name="patch">The patch to apply.</param>
    /// <param name="patternBlock">The pattern block to match.</param>
    public PatternPatcher<TItem> AddPatch(Action<IList<TItem>> patch, params TItem[] patternBlock)
    {
        this.LastPatch = new(patternBlock, patch, false);
        this.Patches.Enqueue(this.LastPatch);
        this.TotalPatches++;

        return this;
    }

    /// <summary>
    ///     Allows patching a list in place after a specific pattern block is matched.
    /// </summary>
    /// <param name="patch">The patch to apply.</param>
    /// <param name="patternBlock">The pattern block to match.</param>
    public void AddPatchLoop(Action<IList<TItem>> patch, params TItem[] patternBlock)
    {
        this.LastPatch = new(patternBlock, patch, true);
        this.Patches.Enqueue(this.LastPatch);
        this.TotalPatches++;
    }

    /// <summary>
    ///     Empty patch that will skip passed the pattern block.
    /// </summary>
    /// <param name="patternBlock">The pattern block to match.</param>
    public PatternPatcher<TItem> AddSeek(params TItem[] patternBlock)
    {
        return this.AddPatch(null, patternBlock);
    }

    public PatternPatcher<TItem> Repeat(int repeat)
    {
        // Add extra copies for repeat-N times patches
        while (--repeat >= 0)
        {
            this.Patches.Enqueue(new(this.LastPatch.Pattern, this.LastPatch.Patch, false));
            this.TotalPatches++;
        }

        return this;
    }

    /// <summary>
    ///     Matches the incoming items against patterns in sequence, and return the patched sequence.
    /// </summary>
    /// <param name="item">The next incoming item from the original list.</param>
    /// <returns>The patched sequence.</returns>
    public IEnumerable<TItem> From(TItem item)
    {
        // Add incoming item to buffer
        this.ItemBuffer.Add(item);

        // No more patches to apply
        if (this.Done)
        {
            return Enumerable.Empty<TItem>();
        }

        // Initialize Patch
        if (this.CurrentPatch is null && this.Patches.TryDequeue(out var patch))
        {
            this.CurrentPatch = patch;
            this.CodeEnum = this.CurrentPatch.Pattern.GetEnumerator();
            this.CodeEnum.MoveNext();
        }

        // No more patches
        if (this.CurrentPatch is null)
        {
            this.Done = true;
            return Enumerable.Empty<TItem>();
        }

        // Does not match current pattern
        if (!this.Comparer(this.CodeEnum.Current, item))
        {
            this.CodeEnum.Reset();
            this.CodeEnum.MoveNext();
            return Enumerable.Empty<TItem>();
        }

        // Matches pattern incompletely
        if (this.CodeEnum.MoveNext())
        {
            return Enumerable.Empty<TItem>();
        }

        // Complete match so apply patch
        this.CurrentPatch.Patch?.Invoke(this.ItemBuffer);
        this.AppliedPatches++;

        // Reset code position to allow looping
        if (this.CurrentPatch.Loop)
        {
            this.CodeEnum.Reset();
            this.CodeEnum.MoveNext();
        }
        else
        {
            // Next patch will be dequeued
            this.CurrentPatch = null;
        }

        // Flush outgoing item buffer
        return this.FlushBuffer();
    }

    /// <summary>
    ///     Returns the remaining buffer of pattern items.
    /// </summary>
    /// <returns>The remaining items in buffer.</returns>
    public IEnumerable<TItem> FlushBuffer()
    {
        foreach (var item in this.ItemBuffer)
        {
            yield return item;
        }

        this.ItemBuffer.Clear();
    }

    private record PatternPatch(IEnumerable<TItem> Pattern, Action<IList<TItem>> Patch, bool Loop);
}