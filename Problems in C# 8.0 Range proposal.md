# Problems in the C# 8.0 Range proposal as of 2019/05/08

This list is not yet complete, but for starters:

## The focus on ranges yet extents are better
Most programming uses extents instead of ranges.  Therefore isn't it a mistake to design this new feature for ranges when actually extents are what we all normally use?   It would be OK if it has good support for _both_ extents and ranges, but if it only supports ranges, then it's a mistake, isn't it?

Hundreds of examples exist to demonstrate that we all use extents rather than ranges, usually.   One of the major examples is `System.Span<T>`.   **Span is designed as an extent not a range.**  Span is an extent because it has a [Length property](https://docs.microsoft.com/en-us/dotnet/api/system.span-1.length) instead of "StartIndex" and "EndIndex" properties.

Span is intended to replace the older `System.ArraySegment<T>`, which was also designed as an extent not a range, as you can see by the fact that ArraySegment contains "Offset" and "Count" properties, not "StartOffset" and "EndOffset" properties.

Hundreds or thousands of examples exist.  Here are a few more:
```
System.String.Substring(int startIndex, int length)
System.Array.Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)
System.Array.Clear(Array array, int index, int length)
System.Array.IndexOf<T>(T[ ] array, T value, int startIndex, int count)
System.Array.BinarySearch<T>(T[ ] array, int index, int length, T value)
.....and many more.....
```

Occasionally in special cases, ranges are needed, but otherwise the normal pattern is to use extents.
Why do we all use extents instead of ranges?  Because **extents are better than ranges in most cases!**   Ranges are generally awkward to use, therefore we all use extents usually (except in special cases where ranges are justified).

This new feature in C# 8.0 is being designed with a strong focus on ranges when in fact extents are better and the standard practice. 


## Awkward to iterate in reverse
A standard "for" loop begins at zero like the following example.  The proposed 1-based design of the `^` operator makes it awkward to iterate in reverse order because it requires that you add 1.
```
char[] data = ...;
int length = data.Length;
for (int i = 0; i < length; i++)
{
    char normalOrder = data[i];
    char reverseOrder = data[^(i + 1)];
    char reverseOrder = data.GetItemReverse(i); // simple; could be supported.
}
```
This iteration example suggests that a reverse-0-based design works better than a reverse-1-based design.


## Pascal-style ordinals/indexes
The current version of the proposal uses Pascal-style ordinals/indexes.  Yes it does.  Really, it does.  Think about it.  It's the indexing scheme of [the Pascal language](https://en.wikipedia.org/wiki/Pascal_(programming_language)).  It is.  Pascal.  But why on earth should Pascal-style ordinals/indexes be resurrected?  In Pascal, the first element/item was 1, but this proved to be problematic, so C changed the idea to first=0.  This issue is already settled for a very long time:  The consensus is that the slim advantage of first=1 is clearly outweighed by the disadvantages, thus first=0 is the winning design, indisputably.  This is not a debatable issue anymore.  It was laid to rest long ago.

Nowadays it's unthinkable to revert back in time to Pascal's ordinals.  Therefore I must ask:  _Why_ is this C# 8 `^0` proposal based on Pascal despite the fact that Pascal's ordinal scheme was ultimately deemed to be a defective design?  Why resurrect this rejected design from the past?

`^0` _is_ Pascal.  The symbol `^` was chosen for this C# 8 proposal because of the approximate similarity in meaning with the normal Exclusive-Or operator `^`.  XOR is kind of like saying "invert" or "reverse" therefore it made sense to reuse this symbol to mean "the other way around" or "in the other direction" in the C# 8 `^0` proposal.  Thus `^i` means "the index i but reversed", and that's fine except the proposal effectively defines `i` in `^i` to be the same as a Pascal ordinal.

In other words, the proposal is that `^i` equals the same as Pascal in reverse order!  Have a look at the identical numbering following and you can see that the `i` in `^i` is a Pascal ordinal.
```
var words = new string[]
{
                //  C/C#  Pascal  Proposed  Pascal_Reverse 
    "The",      //  0     1       ^9        9
    "quick",    //  1     2       ^8        8
    "brown",    //  2     3       ^7        7
    "fox",      //  3     4       ^6        6
    "jumped",   //  4     5       ^5        5
    "over",     //  5     6       ^4        4
    "the",      //  6     7       ^3        3
    "lazy",     //  7     8       ^2        2
    "dog"       //  8     9       ^1        1
};
```

That's why I must ask:  Are you really really _really_ sure that you want to resurrect Pascal's system?  Are you really _really_ **_really_** sure that C# 8 should include BOTH 0-based ordinals AND Pascal-style 1-based ordinals?  I find it quite difficult to believe that it's justifiable to introduce Pascal-reverse-order into C# 8. 


## Unjustified added complexity and performance penalty of negative indexing
The idea of using negative numbers in `System.Range` comes with a price that needs to be carefully considered.  The price is added complexity and a performance penalty.  More research and real-world practical examples are needed in order to determine whether the added complexity and performance penalty of the negative indexes is counterproductive.  

Provided that the total length is supplied via the design/API/syntax, then any range with negative indexes can be converted to a simple extent consisting of offset + length.  The question remains open whether or not it is genuinely worthwhile in real-world scenarios to give `System.Range` the ability to represent negative indexes independently, instead of the simpler alternative of supplying the total length and converting to a simple extent (offset + length).  This question is not very simple to immediately answer.  I think the only way of properly answering it is to collect a number of real-world non-academic examples and convert them to use the proposed Range design and see whether or not the negative indexes are productive or counterproductive.

Some people say that `^0` (`System.Index.End`) has the advantage of being able to represent the end even when the total length is unknown, but `int.MaxValue` is a simpler way of achieving the same thing.  The proposal also supports `^1`, `^2` and so forth, but I haven't seen non-academic examples that demonstrate that this added complexity and performance penalty of negative indexing is not ultimately counterproductive and unnecessary.


## Conflicting meanings of the word "range"
For example, see ["Proposal: Generic ranges or 'any-type' ranges" #2449](https://github.com/dotnet/csharplang/issues/2449).  That person is using a different definition of the word "range" than the people who proposed `System.Range`.  It's not wrong, rather the problem is that the word "range" is ambiguous.  It has multiple different meanings, especially if you include the strange jargon that comes from some mathematicians.  This is one of the reasons why I chose a different name in my proposal ["Advanced Extents & Segments Proposal re C# 8.0 Ranges" #2510](https://github.com/dotnet/csharplang/issues/2510).

A common and valid definition of "range" is like this example:  
> The tile cutting machine has a speed multiplication factor that can be configured in the range of 1.0 .. 2.5.

`System.Range` is using a different definition of this word.  `System.Range` is for the purpose of identifying a segment, slice, or substring of an array or other list of elements.  For example:  
> Copy elements/items 5 .. 10 to the destination object.  

That's why `System.Range` uses integers not floating-point -- because item 2.5 doesn't exist.  Item numbers are ordinals -- integers not fractional numbers.

In my opinion, `System.Range` should be renamed and redesigned, and this would also conveniently avoid the problem of the ambiguous definition of "range".


