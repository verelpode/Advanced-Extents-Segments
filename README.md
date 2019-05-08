# Advanced-Extents-Segments
Rough sketch -- Version 1 (2019/05/07)

The potential for advanced Extents and Segments in C# is exciting and under-estimated.  I am writing this proposal because I feel that the current version of the C# 8.0 Range proposal is not yet ready for release.  It would be sad if a half-baked version is released prematurely and set in concrete forever.  Although future extensions are possible in C# 8.5, this becomes difficult when it breaks compatibility with the syntax and design in C# 8.0, therefore it would be best to design the advanced scenarios NOW not later.

This is a rough sketch and I've been forced to rush it, thus I'm sure it contains some mistakes and omissions, but it is nevertheless worthwhile to present.  Example:

```
int myOffset = ...;
int myLimitOffset = ...;
Segment<char> mySegment = ...;
Segment<char> resultSegment = mySegment[myOffset from start until element is CharMatchers.VerticalWhiteSpace && element != '\u2029' with fence myLimitOffset with maxLength 5000*2];
```

The above example does this:  Beginning with the `Segment<char>` (or `Span<char>` or `Array` or `List<char>`) named "mySegment", make a sub-segment of mySegment (alternatively an Extent alone) that begins at offset "myOffset" from the start, and continues until an element (char) is a vertical whitespace character and does not equal "paragraph separator" (U+2029), but do not extend the Extent or Segment any further than "myLimitOffset", and limit `Extent.Length` to 5000*2.

If you want to get the Extent (offset and length) alone without making a new `Segment<T>` corresponding to that Extent, then you can write:

```
Extent x = [mySegment at myOffset from start until element is CharMatchers.VerticalWhiteSpace && element != '\u2029' with fence myLimitOffset with maxLength 5000*2];
```

Relative offsets:  The "from" keyword makes an offset relative to something else.  The default is "from start" thus writing "myOffset from start" is the same as writing simply "myOffset" without any "from" keyword.  Supported "from" keywords include:
```
	100 from start
	100 from end
	100 from myPositionX forwards
	100 from myPositionX backwards
	100 from myPositionX backwards for 300+10
	100 from last '%' backwards
	100 from first CharMatchers.UppercaseLetter forwards
	100 from first CharMatchers.UppercaseLetter || CharMatchers.WhiteSpace forwards
```

The "for" keyword makes an Extent.  For example, `100 for 20` means `Extent.Offset = 100` and `Extent.Length = 20`.  
`100 from myPositionX backwards for 300+10` means the `Extent.Length` is 300+10, and the `Extent.Offset` is calculated like this:  Begin at offset myPositionX then slide the offset backwards by 100.  The calculation is simply:
```
	Extent.Offset = myPositionX - 100;
	Extent.Length = 300+10;
```

Supported "with" keywords include:
```
	with fence myLimitOffset
	with maxLength myMaxLength
	with minLength myMinLength
	with length myLength    // maybe, see also "for".
	with direction backwards
	with direction forwards
```

`CharMatchers.VerticalWhiteSpace` is an immutable singleton class that implements `IElementMatcher<char>`.  Anyone can write their own classes that implement `IElementMatcher<T>` and match elements using any criteria desired.  

Naturally if you want the opposite of a particular matcher, you can use the logical-not operator with the "is" operator:
```
element is !CharMatchers.Alphabetic && element is !CharMatchers.WhiteSpace
```

Without using a matcher, the following example begins at offset "myStartOffset" and continues until an element (char) is the carriage-return character or the newline character:

```
Segment<char> resultSegment = mySegment[myStartOffset until element == '\r' || element == '\n'];
// Or if you only want the Extent:
Extent x = [mySegment at myStartOffset until element == '\r' || element == '\n'];
```

## "while" keyword
Note the difference between the "until" and "while" keywords.  The following example begins at "myStartOffset" and continues while the elements are decimal digits or underscore:

```
Segment<char> resultSegment = mySegment[myStartOffset while element is CharMatchers.DecimalDigit || element == '_'];
// Or if you only want the Extent:
Extent x = [mySegment at myStartOffset while element is CharMatchers.DecimalDigit || element == '_'];
```

## "where" keyword
The following loop iterates through each segment/run of consecutive whitespace characters:

```
foreach (Segment<char> whitespaceSeg in mySegment[myStartOffset where element is CharMatchers.WhiteSpace])
{ ... }
```

The foreach loops functions with the "where" keyword because "where" produces `IEnumerable<Segment<T>>`:

```
IEnumerable<Segment<char>> results = mySegment[myStartOffset where element is CharMatchers.WhiteSpace];
```

Note that the returned `IEnumerable` instance is not a pre-generated array, rather it produces its enumerable elements on-demand like how `System.Linq.Enumerable` does.  This can be implemented using the preexisting `yield return` feature of C#.  Also on the topic of efficiency, note that `Segment<T>` is a struct, thus the `where` keyword does not create thousands of objects in the heap.

TO DO:  The "where" keyword gives you the matching segments.  Decide whether to make an option or variation of "where" 
that gives you both the matching and non-matching segments.


## Moving Average 
A "moving average" example was provided on the following webpage, but the version there as of 2019/05/07 looks defective unfortunately.
	https://docs.microsoft.com/en-us/dotnet/csharp/tutorials/ranges-indexes
Many elements are missed/ignored in that moving average because it does `start += 100" yet "Range r = start..start+10;`
Furthermore, it has a function named "MovingAverage" that doesn't return a MovingAverage at all, surprisingly.  It is named MovingAverage but it returns the same as `System.Linq.Enumerable.Average`.

The following design of MovingAverage attempts to eliminate the defects.  In addition, it is better because it smooths-out the average by overlapping the segments.

```
using System.Linq.Enumerable;
static IEnumerable<double> MovingAverage(Segment<int> inData)
{
	foreach (Segment<int> seg in inData.CutAndSplice(inPieceLength: 20, inOverlapLength: 10))
	{
		yield return seg.Average();
	}
}
```

See also:  https://en.wikipedia.org/wiki/Moving_average


## Factionalize 
The "factionalize" operation splits a segment into a sequence of "factionalized segments".  A "factionalized segment" is a `Segment<T>` with a faction value assigned.  For example, factionalization could be used as a stage in syntax parsing of text that is formatted to obey particular syntax rules. Example:

```
enum MyFaction { ... }
Segment<char> mySegment = ...;
foreach (FactionalizedSegment<char,MyFaction> in mySegment.Factionalize<MyFaction>(MyFactionalizer))
{ ... }
```

The foreach loops functions with `Segment<T>.Factionalize` because `Factionalize` returns `IEnumerable<FactionalizedSegment<TElement,TFaction>>`.

If `Factionalize` is used as a stage in syntax parsing of a programming, scripting, or data language, then `TFaction` could be defined as an enumeration like this:

```
enum TokenKind
{
	None = 0,
	WhiteSpace,
	Number,
	Word,
	Keyword,
	PlusSymbol,
	MinusSymbol,
	MultiplySymbol,
	...
}
```

An instance of the delegate `DlgFactionalizer` is given to the `Segment<T>.Factionalize` method.  See definition of this delegate within the .cs file.

## Other slicing operations
See the .cs file for other slicing operations in `Segment<T>` such as:
  
```
public IEnumerable<Segment<T>> CutUp(int inPieceLength)
public static IEnumerable<Segment<T>> operator / (Segment<T> inSegment, int inPieceLength)
public IEnumerable<Segment<T>> CutAndSplice(int inPieceLength, int inOverlapLength)
public IEnumerable<Segment<T>> Dice(int inPieceLength, int inSkipLength, bool inInvert = false)
public IEnumerable<Segment<T>> Split(DlgSegmentSplitter<T> inSplitter)
public static IEnumerable<Segment<T>> Interleave(int inLength, params Segment<T>[] inSegments)
public Segment<T> ChopOffStart(int inPieceLength)
public Segment<T> ChopOffEnd(int inPieceLength)
public Segment<T> CleaveStart(int inPieceLength)
public Segment<T> CleaveEnd(int inPieceLength)
public void SeverStart(int inPieceLength, out Segment<T> outStartPiece, out Segment<T> outRemainder)
public void SeverEnd(int inPieceLength, out Segment<T> outEndPiece, out Segment<T> outRemainder)
public void Sever(Extent inExtent, out Segment<T> outStart, out Segment<T> outMiddle, out Segment<T> outEnd)
public (Segment<T> partA, Segment<T> partB) Bisect(int inOffset)
public IEnumerable<FactionalizedSegment<T,TFaction>> Factionalize<TFaction>(DlgFactionalizer<T,TFaction> inFactionalizer)
public Segment<T> Truncate(int inMaxLength)
public Segment<T> GetExtent(Extent inExtent)
public Segment<T> GetExtent(int inOffset, int inLength)
public Segment<T> GetExtentToEnd(int inStartOffset)
public Segment<T> GetExtentToStart(int inOffset)
public Segment<T> GetExtentAtStart(int inLength)  // might be renamed to GetPrefix
public Segment<T> GetExtentAtEnd(int inLength)    // might be renamed to GetSuffix
public Segment<T> GetRange(int inStartOffset, int inEndOffset)
public Segment<T> GetRangeInclusive(int inStartOffset, int inEndOffset)
```

## Syntax for basic extents and ranges
Basic examples following (basic meaning not using the `until`, `while` keywords etc).


```
Extent x = [20 for 30];   // makes Extent.Offset=20, Extent.Length=30
Extent rangeA = [20..50]; // makes Extent.Offset=20, Extent.EndOffset=50
Extent rangeToEnd = [20..];
Extent rangeToStart = [..20];
Extent singleElement = [5];  // makes Extent.Offset=5, Extent.Length=1 or maybe Length=0
Extent negativeOrdinal = -[5]; // Extent.Offset == DataLength - 5
Extent reverseOrdinal = ~[5];  // makes 0-based ordinal in reverse, as if System.Array.Reverse was executed.
Extent rangeB = [20 .. -[5]];  // Extent.EndOffset == DataLength - 5
```

The following are the same, which means that `-[5]` actually invokes the `operator - (Extent)` overload method that is defined in struct Extent.
```
Extent negativeOrdinal = -[5];
Extent negativeOrdinal = -(new Extent(5, 1));
```

Likewise `~[5]` invokes the `operator ~ (Extent)` overload method that is defined in struct Extent.


## Iteration in reverse order
Example:
```
Segment<char> seg = ...;
int len = seg.Length;
for (int i = 0; i < len; i++)
{
	char ch = seg.InReverse[i];
	// Also supported:
	char ch = seg.GetItemReverse(i);
}
```

## Builder
See `Segment<T>.Builder` in the .cs file.
