/*
================================================================================
Advanced Extents and Segments by Verelpode
Rough sketch -- Version 1 (2019/05/07)
================================================================================

The potential for advanced Extents and Segments in C# is exciting and under-estimated.  I am writing this proposal because 
I feel that the current version of the C# 8.0 Range proposal is not yet ready for release.  It would be sad if a half-baked 
version is released prematurely and set in concrete forever.  Although future extensions are possible in C# 8.5, this becomes
difficult when it breaks compatibility with the syntax and design in C# 8.0, therefore it would be best to design the 
advanced scenarios NOW not later.

This is a rough sketch and I've been forced to rush it, thus I'm sure it contains some mistakes and omissions, but it is 
nevertheless worthwhile to present.  Example:

	int myOffset = ...;
	int myLimitOffset = ...;
	Segment<char> mySegment = ...;
	Segment<char> resultSegment = mySegment[myOffset from start until element is CharMatchers.VerticalWhiteSpace && element != '\u2029' with fence myLimitOffset with maxLength 5000*2];

The above example does this:  Beginning with the Segment<char> (or Span<char> or Array or List<char>) named "mySegment", make a 
sub-segment of mySegment (alternatively an Extent alone) that begins at offset "myOffset" from the start, and continues until an 
element (char) is a vertical whitespace character and does not equal "paragraph separator" (U+2029), but do not extend the Extent 
or Segment any further than "myLimitOffset", and limit Extent.Length to 5000*2.

If you want to get the Extent (offset and length) alone without making a new Segment<T> corresponding to that Extent, then you can write:

	Extent x = [mySegment at myOffset from start until element is CharMatchers.VerticalWhiteSpace && element != '\u2029' with fence myLimitOffset with maxLength 5000*2];

Relative offsets:  The "from" keyword makes an offset relative to something else.  The default is "from start" thus writing 
"myOffset from start" is the same as writing simply "myOffset" without any "from" keyword.  Supported "from" keywords include:
	100 from start
	100 from end
	100 from myPositionX forwards
	100 from myPositionX backwards
	100 from myPositionX backwards for 300+10
	100 from last '%' backwards
	100 from first CharMatchers.UppercaseLetter forwards
	100 from first CharMatchers.UppercaseLetter || CharMatchers.WhiteSpace forwards

The "for" keyword makes an Extent.  For example, "100 for 20" means Extent.Offset = 100 and Extent.Length = 20.  
"100 from myPositionX backwards for 300+10" means the Extent.Length is 300+10, and the Extent.Offset is calculated like this:
Begin at offset myPositionX then slide the offset backwards by 100.  The calculation is simply:
	Extent.Offset = myPositionX - 100;
	Extent.Length = 300+10;

Supported "with" keywords include:
	with fence myLimitOffset
	with maxLength myMaxLength
	with minLength myMinLength
	with length myLength    // maybe, see also "for".
	with direction backwards
	with direction forwards

CharMatchers.VerticalWhiteSpace is an immutable singleton class that implements IElementMatcher<char>.  Anyone can write 
their own classes that implement IElementMatcher<T> and match elements using any criteria desired.  

Naturally if you want the opposite of a particular matcher, you can use the logical-not operator with the "is" operator:
	element is !CharMatchers.Alphabetic && element is !CharMatchers.WhiteSpace

Without using a matcher, the following example begins at offset "myStartOffset" and continues until an element (char) is 
the carriage-return character or the newline character:

	Segment<char> resultSegment = mySegment[myStartOffset until element == '\r' || element == '\n'];
	// Or if you only want the Extent:
	Extent x = [mySegment at myStartOffset until element == '\r' || element == '\n'];


========= "while" keyword =========
Note the difference between the "until" and "while" keywords.  The following example begins at "myStartOffset" and continues while the elements are
decimal digits or underscore:

	Segment<char> resultSegment = mySegment[myStartOffset while element is CharMatchers.DecimalDigit || element == '_'];
	// Or if you only want the Extent:
	Extent x = [mySegment at myStartOffset while element is CharMatchers.DecimalDigit || element == '_'];


========= "where" keyword =========
The following loop iterates through each segment/run of consecutive whitespace characters:

	foreach (Segment<char> whitespaceSeg in mySegment[myStartOffset where element is CharMatchers.WhiteSpace])
	{ ... }

The foreach loops functions with the "where" keyword because "where" produces IEnumerable<Segment<T>>:

	IEnumerable<Segment<char>> results = mySegment[myStartOffset where element is CharMatchers.WhiteSpace];

Note that the returned IEnumerable instance is not a pre-generated array, rather it produces its enumerable elements 
on-demand like how System.Linq.Enumerable does.  This can be implemented using the preexisting "yield return" feature 
of C#.  Also on the topic of efficiency, note that Segment<T> is a struct, thus the "where" keyword does not create 
thousands of objects in the heap.

TO DO:  The "where" keyword gives you the matching segments.  Decide whether to make an option or variation of "where" 
that gives you both the matching and non-matching segments.


========= Moving Average =========
A "moving average" example was provided on the following webpage, but the version there as of 2019/05/07 looks defective unfortunately.
	https://docs.microsoft.com/en-us/dotnet/csharp/tutorials/ranges-indexes
Many elements are missed/ignored in that moving average because it does "start += 100" yet "Range r = start..start+10;"
Furthermore, it has a function named "MovingAverage" that doesn't return a MovingAverage at all, surprisingly.  It is named MovingAverage 
but it returns the same as System.Linq.Enumerable.Average.

The following design of MovingAverage attempts to eliminate the defects.  In addition, it is better because it smooths-out 
the average by overlapping the segments.

	using System.Linq.Enumerable;
	static IEnumerable<double> MovingAverage(Segment<int> inData)
	{
		foreach (Segment<int> seg in inData.CutAndSplice(inPieceLength: 20, inOverlapLength: 10))
		{
			yield return seg.Average();
		}
	}

See also:  https://en.wikipedia.org/wiki/Moving_average


========= Factionalize =========
The "factionalize" operation splits a segment into a sequence of "factionalized segments".  A "factionalized segment" 
is a Segment<T> with a faction value assigned.  For example, factionalization could be used as a stage in syntax
parsing of text that is formatted to obey particular syntax rules.
Example:

	enum MyFaction { ... }
	Segment<char> mySegment = ...;
	foreach (FactionalizedSegment<char,MyFaction> in mySegment.Factionalize<MyFaction>(MyFactionalizer))
	{ ... }

The foreach loops functions with Segment<T>.Factionalize because Factionalize returns IEnumerable<FactionalizedSegment<TElement,TFaction>>.

If Factionalize is used as a stage in syntax parsing of a programming, scripting, or data language, then TFaction could be 
defined as an enumeration like this:

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

An instance of the delegate "DlgFactionalizer" is given to the Segment<T>.Factionalize method.  See definition of 
this delegate within this .cs file.

========= Other slicing operations =========
See the .cs file for other slicing operations in Segment<T> such as:

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


========= Syntax for basic extents and ranges =========
Basic examples following (basic meaning not using the "until", "while" keywords etc).

	Extent x = [20 for 30];   // makes Extent.Offset=20, Extent.Length=30
	Extent rangeA = [20..50]; // makes Extent.Offset=20, Extent.EndOffset=50
	Extent rangeToEnd = [20..];
	Extent rangeToStart = [..20];
	Extent singleElement = [5];  // makes Extent.Offset=5, Extent.Length=1 or maybe Length=0
	Extent negativeOrdinal = -[5]; // Extent.Offset == DataLength - 5
	Extent reverseOrdinal = ~[5];  // makes 0-based ordinal in reverse, as if System.Array.Reverse was executed.
	Extent rangeB = [20 .. -[5]];  // Extent.EndOffset == DataLength - 5

The following are the same, which means that "-[5]" actually invokes the "operator - (Extent)" overload method that is defined in struct Extent.

	Extent negativeOrdinal = -[5];
	Extent negativeOrdinal = -(new Extent(5, 1));

Likewise "~[5]" invokes the "operator ~ (Extent)" overload method that is defined in struct Extent.


========= Iteration in reverse order =========
Example:

	Segment<char> seg = ...;
	int len = seg.Length;
	for (int i = 0; i < len; i++)
	{
		char ch = seg.InReverse[i];
		// Also supported:
		char ch = seg.GetItemReverse(i);
	}


========= Builder =========
See Segment<T>.Builder in the .cs file.

*/



using System;
using System.Collections.Generic;

namespace AdvancedSegments
{

	public readonly struct Extent : IEquatable<Extent>, IComparable<Extent>
	{
		// TO DO: Decide whether to tweak these fields by interpreting them specially when fOffset and/or fLength is negative.
		private readonly int fOffset, fLength;

		public Extent(int inOffset, int inLength)
		{
			fOffset = inOffset;
			fLength = inLength;
			// TO DO: Decide whether Extent constructor should throw exception when inOffset and/or inLength is negative.
		}

		// Inclusive of the element at inStartOffset and exclusive of the element at inEndOffset.
		public static Extent NewRange(int inStartOffset, int inEndOffset)
		{
			if (inStartOffset < 0 | inStartOffset > inEndOffset) throw new Exception("xxxxxx");
			return new Extent(inStartOffset, unchecked(inEndOffset - inStartOffset));
		}

		// Inclusive of both the elements at inStartOffset and inEndOffset.
		public static Extent NewRangeInclusive(int inStartOffset, int inEndOffset)
		{
			if (inStartOffset < 0 | inStartOffset > inEndOffset) throw new Exception("xxxxxx");
			return new Extent(inStartOffset, checked((inEndOffset + 1) - inStartOffset));
		}

		public static Extent NewToEnd(int inStartOffset)
		{
			return new Extent(inStartOffset, int.MaxValue);
		}
		public static Extent NewToStart(int inOffset)
		{
			return new Extent(0, inOffset);
		}

		public static Extent Entire
		{
			get {
				// Alternatively fLength == -1 could mean Entire but this is an internal implementation detail.
				return new Extent(0, int.MaxValue);
			}
		}

		public bool IsEntire
		{
			get {
				return fOffset == 0 && fLength == int.MaxValue;
			}
		}

		public bool IsToEnd
		{
			get {
				return fLength == int.MaxValue; ;
			}
		}

		public static Extent Empty
		{
			get { return default(Extent); }
		}

		public bool IsEmpty
		{
			get { return fLength <= 0; }
		}
		public bool IsNotEmpty
		{
			get { return fLength > 0; }
		}
		// TO DO: Decide whether to keep, delete, or modify operator true and operator false.  Note these operators also allow logical AND and logical OR operators to be used.  See also operator "!" overload.
		public static bool operator true(Extent inExtent)
		{
			return inExtent.IsNotEmpty;
		}
		public static bool operator false(Extent inExtent)
		{
			return inExtent.IsEmpty;
		}


		public int Offset
		{
			get { return fOffset; }
		}

		public int EndOffset
		{
			get { return fOffset + fLength; }
		}

		public int Length
		{
			get { return fLength; }
		}

		public Extent Truncate(int inMaxLength)
		{
			if (inMaxLength < 0) inMaxLength = 0;
			if (inMaxLength < fLength) return new Extent(fOffset, inMaxLength);
			return this;
		}

		public Extent ChangeLength(int inDelta)
		{
			int newLen = checked(fLength + inDelta);
			if (newLen < 0) throw new Exception("xxxxxx");
			return new Extent(fOffset, newLen);
		}

		public static Extent operator + (Extent inExtent, int inLengthDelta)
		{
			return inExtent.ChangeLength(inLengthDelta);
		}

		public static Extent operator - (Extent inExtent, int inLengthDelta)
		{
			int newLen = checked(inExtent.fLength - inLengthDelta);
			if (newLen < 0) throw new Exception("xxxxxx");
			return new Extent(inExtent.fOffset, newLen);
		}

		public static Extent operator ++ (Extent inExtent)
		{
			int newLen = checked(inExtent.fLength + 1);
			if (newLen < 0) throw new Exception("xxxxxx");
			return new Extent(inExtent.fOffset, newLen);
		}
		public static Extent operator -- (Extent inExtent)
		{
			int newLen = checked(inExtent.fLength - 1);
			if (newLen < 0) throw new Exception("xxxxxx");
			return new Extent(inExtent.fOffset, newLen);
		}


		public Extent GetPrefix(int inPieceLength)
		{
			int oldLen = fLength;
			if (inPieceLength > oldLen) inPieceLength = oldLen;
			if (inPieceLength < 0) inPieceLength = 0;
			return new Extent(fOffset, inPieceLength);
		}


		// Returns inExtentA excluding any part of inExtentA that overlaps inExtentB.
		public static (Extent, Extent) operator - (Extent inExtentA, Extent inExtentB)
		{
			Extent sect = GetIntersection(inExtentA, inExtentB);
			if (sect.IsEmpty) return (inExtentA, Extent.Empty);
			throw new NotImplementedException();
		}

		public static Extent operator + (Extent inExtentA, Extent inExtentB)
		{
			return GetUnion(inExtentA, inExtentB);
		}



		public static Extent operator ~ (Extent inExtent)
		{
			// TO DO: operator ~ could flip between ordinal-in-reverse and normal (forwards) ordinal.  Decide whether this is genuinely a productive or counterproductive feature.
			// Alternatively operator ~ could mean inversion of the Extent provided that it returns 2 Extents:  public static (Extent, Extent) operator ~ (Extent inExtent) { ... }
			throw new NotImplementedException();
		}
		/*
		public static Extent operator ! (Extent inExtent)
		{
			// TO DO: Decide whether to make operator ! or operator ~ (or neither) flip between ordinal-in-reverse and normal (forwards) ordinal.
			// Note operator ! should not conflict with operator true, operator false.
		}
		*/

		public static Extent operator - (Extent inExtent)
		{
			// TO DO: The negation operator (arity 1) might make negative ordinals meaning Length - i.   Decide whether this is genuinely a productive or counterproductive feature.
			throw new NotImplementedException();
		}
		/*
		public static Extent operator + (Extent inExtent)
		{
			// TO DO: If the negation operator makes negative ordinals, then operator + (arity 1) might change from negative ordinals to normal ordinals.
		}
		*/


		public Extent Move(int inDelta)
		{
			// TO DO: Decide whether to allow negative offset result versus throw exception.
			return new Extent(checked(fOffset + inDelta), fLength);
		}

		public Extent MoveBackwards(int inDelta)
		{
			// TO DO: Decide whether to allow negative offset result versus throw exception.
			return new Extent(checked(fOffset - inDelta), fLength);
		}

		public static Extent operator >> (Extent inExtent, int inDelta)
		{
			return inExtent.Move(inDelta);
		}

		public static Extent operator << (Extent inExtent, int inDelta)
		{
			return inExtent.MoveBackwards(inDelta);
		}

		public Extent MoveTo(int inNewOffset)
		{
			return new Extent(inNewOffset, fLength);
		}


		public static Extent operator | (Extent inExtentA, Extent inExtentB)
		{
			return GetUnion(inExtentA, inExtentB);
		}

		public static Extent operator & (Extent inExtentA, Extent inExtentB)
		{
			return GetIntersection(inExtentA, inExtentB);
		}

		public static (Extent, Extent) operator ^ (Extent inExtentA, Extent inExtentB)
		{
			// Return symmetric difference of the 2 extents.  See also System.Collections.Generic.HashSet<T>.SymmetricExceptWith(IEnumerable<T>).
			throw new NotImplementedException();
		}
	
		public static Extent GetIntersection(Extent inExtentA, Extent inExtentB)
		{
			int sectStart = Math.Max(inExtentA.fOffset, inExtentB.fOffset);
			int sectEnd = Math.Min(inExtentA.EndOffset, inExtentB.EndOffset);
			if (sectStart < 0 | sectEnd < 0) throw new Exception("xxxxxx");
			if (sectStart < sectEnd) return new Extent(sectStart, unchecked(sectEnd - sectStart));
			return Extent.Empty;
		}

		public bool Intersects(Extent inOther)
		{
			return Math.Max(this.fOffset, inOther.fOffset) < Math.Min(this.EndOffset, inOther.EndOffset);
		}

		public static Extent GetUnion(Extent inExtentA, Extent inExtentB)
		{
			// Calculate the the smallest extent that includes both inExtentA and inExtentB.
			throw new NotImplementedException();
		}

		public bool Contains(Extent inOther)
		{
			int aOffset = this.fOffset;
			int bOffset = inOther.fOffset;
			int aEndOffset = aOffset + this.fLength;
			int bEndOffset = bOffset + inOther.fLength;
			return (bOffset >= aOffset) && (bOffset < aEndOffset) && (bEndOffset <= aEndOffset) && (bEndOffset >= aOffset);
		}

		/// <summary>Constrains/chops the Exetent to make it contained entirely within the specified other Extent.</summary>
		public Extent Constrain(Extent inEnclosure)
		{
			throw new NotImplementedException();
		}

		

		public (Extent partA, Extent partB) Bisect(int inOffset)
		{
			int len = fLength;
			if (inOffset > len) inOffset = len;
			if (inOffset < 0) inOffset = 0;
			int existingOfs = fOffset;
			int partBOffset = existingOfs + inOffset;
			return (new Extent(existingOfs, inLength: inOffset), (len == int.MaxValue) ? Extent.NewToEnd(partBOffset) : new Extent(partBOffset, len - inOffset));
		}

		public bool IsValid(int inTotalLength)
		{
			return Extent.IsValid(fOffset, fLength, inTotalLength);
		}

		public static bool IsValid(int inOffset, int inLength, int inTotalLength)
		{
			unchecked {
				if (inOffset < 0 | inLength < 0 | inTotalLength < 0) return false;
				uint ofs = (uint)inOffset;
				uint cnt = (uint)inLength;
				uint totalLength = (uint)inTotalLength;
				uint endOffset = ofs + cnt; // may overflow.
				return (ofs <= totalLength) && (endOffset <= totalLength) && (endOffset >= ofs);	// the last condition checks for overflow.
			} // unchecked
		}

		// If the range is empty but otherwise valid, returns true.
		// If IsValidRange returns true, you can safely calculate the range length like this:   length = unchecked(inEndOffset - inStartOffset)
		public static bool IsValidRange(int inStartOffset, int inEndOffset, int inTotalLength)
		{
			// Checking (inEndOffset >= 0) is unnecessary because of (inEndOffset >= inStartOffset)
			// Checking (inStartOffset <= inTotalLength) is unnecessary because of (inEndOffset >= inStartOffset) and (inEndOffset <= inTotalLength)
			return (inStartOffset >= 0 && inEndOffset >= inStartOffset && inTotalLength >= 0 && inEndOffset <= inTotalLength);
		}

		/// <summary>Returns a valid version of the Extent, make valid by chopping off any parts that prior to offset zero or exceeding the specified total length.</summary>
		public Extent Validate(int inTotalLength)
		{
			throw new NotImplementedException();
		}



		public bool Equals(Extent inOther)
		{
			return this.fOffset == inOther.fOffset && this.fLength == inOther.fLength;
		}
		public override bool Equals(object inOther)
		{
			if (inOther is Extent otherExtent) return this.Equals(otherExtent);
			return false;
		}
		public static bool operator == (Extent inExtentA, Extent inExtentB)
		{
			return inExtentA.Equals(inExtentB);
		}
		public static bool operator != (Extent inExtentA, Extent inExtentB)
		{
			return !inExtentA.Equals(inExtentB);
		}

		public override int GetHashCode()
		{
			return fOffset ^ fLength;
		}

		public int CompareTo(Extent inOther) // implements IComparable<Extent>
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			int ofs = fOffset;
			int len = fLength;
			if (len == int.MaxValue) return string.Concat("[", ofs.ToString(), "..]");
			return string.Concat("[", ofs.ToString(), " for ", len.ToString(), "]");
		}


	} // struct Extent


	/* ======================================================================================================================= */
	/* ======================================================================================================================= */
	/* ======================================================================================================================= */


	public readonly struct Segment<T> : IReadOnlyList<T>, IEquatable<Segment<T>>, IComparable<Segment<T>>
	{
		private readonly ISegmentable<T> fSourceData;
		private readonly Extent fExtent;

		public Segment(ISegmentable<T> inSourceData, Extent inExtent)
		{
			fSourceData = inSourceData;
			fExtent = inExtent;
			// TO DO: Decide whether Segment constructor should throw an exception when inExtent is invalid in regards to inSourceData.Length.
			// In comparison, the constructor of System.ArraySegment throws ArgumentOutOfRangeException or ArgumentException when the extent is invalid.
			// However see frex this.Truncate where it invokes the constructor and it already validated the length.
		}

		public Segment(ISegmentable<T> inSourceData, int inOffset, int inLength)
		{
			fSourceData = inSourceData;
			fExtent = new Extent(inOffset, inLength);
			// TO DO: Decide whether Segment constructor should throw an exception when inExtent is invalid in regards to inSourceData.Length.
		}

		/// <summary>Constructs a Segment that starts at the specified offset and continues until the end of the source data.</summary>
		public Segment(ISegmentable<T> inSourceData, int inOffset)
		{
			// TO DO: Decide whether to throw exception when inOffset is invalid versus to clamp it to the nearest valid offset and continue.
			int totalLen = (inSourceData is null) ? 0 : inSourceData.Length;
			if (inOffset < 0 | inOffset > totalLen) throw new Exception("xxxxxxx");
			fSourceData = inSourceData;
			fExtent = new Extent(inOffset, unchecked(totalLen - inOffset));
			//fOffset = inOffset;
			//fLength = unchecked(totalLen - inOffset);
		}

		public Segment(ISegmentable<T> inSourceData)
		{
			fSourceData = inSourceData;
			fExtent = Extent.Entire;
			// TO DO: Decide whether to set fExtent to Extent.Entire or:  fExtent = new Extent(0, (inSourceData is null) ? 0 : inSourceData.Length);
			/*
			If Segment<T> is internally implemented with fOffset and fLength fields instead of fExtent field, then do:
			fOffset = 0;
			fLength = (inSourceData is null) ? 0 : inSourceData.Length;
			*/
		}

		// See also this.GetExtentAtStart.
		public static Segment<T> NewPrefix(ISegmentable<T> inSourceData, int inLength)
		{
			int totalLen = (inSourceData is null) ? 0 : inSourceData.Length;
			if (inLength > totalLen) inLength = totalLen;
			if (inLength < 0) inLength = 0;
			// TO DO:  Consider whether to throw exception:  if (inLength < 0 | inLength > totalLen) throw new Exception("xxxxxx");
			return new Segment<T>(inSourceData, 0, inLength);
		}

		// See also this.GetExtentAtEnd.
		public static Segment<T> NewSuffix(ISegmentable<T> inSourceData, int inLength)
		{
			int totalLen = (inSourceData is null) ? 0 : inSourceData.Length;
			if (inLength > totalLen) inLength = totalLen;
			if (inLength < 0) inLength = 0;
			// TO DO:  Consider whether to throw exception:  if (inLength < 0 | inLength > totalLen) throw new Exception("xxxxxx");
			return new Segment<T>(inSourceData, unchecked(totalLen - inLength), inLength);
		}

		// See also this.GetExtentToEnd.
		public static Segment<T> NewToEnd(ISegmentable<T> inSourceData, int inStartOffset)
		{
			return new Segment<T>(inSourceData, inStartOffset);
		}
		public static Segment<T> NewToStart(ISegmentable<T> inSourceData, int inOffset)
		{
			return new Segment<T>(inSourceData, 0, inLength: inOffset);
		}

		// Inclusive of the element at inStartOffset and exclusive of the element at inEndOffset.
		// See also this.GetRange.
		public static Segment<T> NewRange(ISegmentable<T> inSourceData, int inStartOffset, int inEndOffset)
		{
			if (!Extent.IsValidRange(inStartOffset, inEndOffset, (inSourceData is null) ? 0 : inSourceData.Length))
				throw new Exception("xxxxxx");
			return new Segment<T>(inSourceData, inStartOffset, unchecked(inEndOffset - inStartOffset));
		}

		// Inclusive of both the elements at inStartOffset and inEndOffset.
		// See also this.GetRangeInclusive.
		public static Segment<T> NewRangeInclusive(ISegmentable<T> inSourceData, int inStartOffset, int inEndOffset)
		{
			if (inStartOffset > inEndOffset) throw new Exception("xxxxxx");
			return NewRange(inSourceData, inStartOffset, checked(inEndOffset + 1));
		}




		public static Segment<T> Empty
		{
			get { return default(Segment<T>); }
		}

		public ISegmentable<T> SourceData
		{
			get { return fSourceData; }
		}

		public int Offset
		{
			get { return fExtent.Offset; }
		}

		public int EndOffset
		{
			get { return fExtent.EndOffset; }
		}

		public int Length
		{
			get { return fExtent.Length; }
		}
		int IReadOnlyCollection<T>.Count { get { return fExtent.Length; } }

		public Segment<T> Truncate(int inMaxLength)
		{
			if (inMaxLength < 0) inMaxLength = 0;
			if (inMaxLength < this.Length) return new Segment<T>(this.fSourceData, this.Offset, inMaxLength);
			return this;
		}

		public Segment<T> ChangeLength(int inDelta)
		{
			if (inDelta == 0) return this;
			int newLen = checked(this.Length + inDelta);
			int ofs = this.Offset;
			var sd = fSourceData;
			if (!Extent.IsValid(ofs, newLen, (sd is null) ? 0 : sd.Length)) throw new Exception("xxxxxx");
			return new Segment<T>(sd, ofs, newLen);
		}


		public Segment<T> this[Extent inExtent]
		{
			get {
				return this.GetExtent(inExtent);
			}
		}

		public Segment<T> GetExtent(Extent inExtent)
		{
			return this.GetExtent(inExtent.Offset, inExtent.Length);
		}

		public Segment<T> GetExtent(int inOffset, int inLength)
		{
			if (!Extent.IsValid(inOffset, inLength, this.Length)) throw new Exception("xxxxxx");
			return new Segment<T>(fSourceData, this.Offset + inOffset, inLength);
		}

		public Segment<T> GetExtentToEnd(int inStartOffset)
		{
			// GetExtentToEnd(inStartOffset) should be same result as:  this.GetExtent(inStartOffset, this.Length - inStartOffset);
			// TO DO: Decide whether to throw exception when inStartOffset is invalid versus to clamp it.
			int oldLen = this.Length;
			if (inStartOffset < 0 | inStartOffset > oldLen) throw new Exception("xxxxxx");
			return new Segment<T>(fSourceData, this.Offset + inStartOffset, oldLen - inStartOffset);
		}
		public Segment<T> GetExtentToStart(int inOffset)
		{
			return this.GetExtent(0, inLength: inOffset);
		}

		public Segment<T> GetExtentAtStart(int inLength)
		{
			// TO DO: Decide whether to rename GetExtentAtStart to GetPrefix and GetExtentAtEnd to GetSuffix.  See also NewPrefix and NewSuffix.
			// TO DO: Decide whether to throw exception when inLength is invalid versus to clamp it as follows:
			int oldLen = this.Length;
			if (inLength > oldLen) inLength = oldLen;
			if (inLength < 0) inLength = 0;
			return new Segment<T>(this.fSourceData, this.Offset, inLength);
		}

		public Segment<T> GetExtentAtEnd(int inLength)
		{
			// TO DO: Decide whether to rename GetExtentAtStart to GetPrefix and GetExtentAtEnd to GetSuffix.  See also NewPrefix and NewSuffix.
			// TO DO: Decide whether to throw exception when inLength is invalid versus to clamp it as follows:
			int oldLen = this.Length;
			if (inLength > oldLen) inLength = oldLen;
			if (inLength < 0) inLength = 0;
			return new Segment<T>(this.fSourceData, this.Offset + oldLen - inLength, inLength);
		}

		// Inclusive of the element at inStartOffset and exclusive of the element at inEndOffset.
		public Segment<T> GetRange(int inStartOffset, int inEndOffset)
		{
			if (!Extent.IsValidRange(inStartOffset, inEndOffset, this.Length))
				throw new Exception("xxxxxx");
			return new Segment<T>(fSourceData, this.Offset + inStartOffset, unchecked(inEndOffset - inStartOffset));
		}

		// Inclusive of both the elements at inStartOffset and inEndOffset.
		public Segment<T> GetRangeInclusive(int inStartOffset, int inEndOffset)
		{
			if (inStartOffset > inEndOffset) throw new Exception("xxxxxx");
			return this.GetRange(inStartOffset, checked(inEndOffset + 1));
		}


		public T this[int inOrdinal]
		{
			get
			{
				if (inOrdinal >= 0 && inOrdinal < this.Length) return fSourceData[this.Offset + inOrdinal];
				throw new ArgumentOutOfRangeException();
			}
			set
			{
				this.SetItem(inOrdinal, value);
			}
		}

		public void SetItem(int inOrdinal, T inValue)
		{
			if (inOrdinal >= 0 && inOrdinal < this.Length)
				fSourceData[this.Offset + inOrdinal] = inValue;
			else
				throw new ArgumentOutOfRangeException();
		}

		public T GetItemOrDefault(int inOrdinal, T inDefault = default(T))
		{
			int len = this.Length;
			if (inOrdinal >= 0 && inOrdinal < len) return this[inOrdinal];
			return inDefault;
		}

		public T GetFirstItemOrDefault(T inDefault = default(T))
		{
			if (this.Length > 0) return this[0];
			return inDefault;
		}

		public T GetLastItemOrDefault(T inDefault = default(T))
		{
			int len = this.Length;
			if (len > 0) return this[len - 1];
			return inDefault;
		}


		/*
		This InReverse property allows you to write frex:
			Segment<char> seg = ...;
			int len = seg.Length;
			for (int i = 0; i < len; i++)
			{
				char ch = seg.InReverse[i];
				// Also supported:
				char ch = seg.GetItemReverse(i);
			}
		TO DO:  Decide whether to keep InReverse property versus delete it and use Segment<T>.GetItemReverse instead.
		See also System.Array.Reverse(System.Array array, int index, int length);
		*/
		public SegmentInReverse<T> InReverse
		{
			get { return new SegmentInReverse<T>(this); }
		}

		public T GetItemReverse(int inOrdinal)
		{
			int len = this.Length;
			if (inOrdinal >= 0 && inOrdinal < len) return this[len - inOrdinal - 1];
			throw new Exception("xxxxxx");
		}

		public T GetItemReverseOrDefault(int inOrdinal, T inDefault = default(T))
		{
			int len = this.Length;
			if (inOrdinal >= 0 && inOrdinal < len) return fSourceData[this.Offset + len - inOrdinal - 1];
			return inDefault;
		}

		public void SetItemReverse(int inOrdinal, T inValue)
		{
			int len = this.Length;
			if (inOrdinal >= 0 && inOrdinal < len)
				fSourceData[this.Offset + len - inOrdinal - 1] = inValue;
			else
				throw new Exception("xxxxxx");
		}

		public T GetItemFromEnd(int inOffset)
		{
			int len = this.Length;
			if (inOffset > 0 && inOffset <= len) return fSourceData[this.Offset + len - inOffset];
			throw new Exception("xxxxxx");
		}

		public void SetItemFromEnd(int inOffset, T inValue)
		{
			int len = this.Length;
			if (inOffset > 0 && inOffset <= len)
				fSourceData[this.Offset + len - inOffset] = inValue;
			else
				throw new Exception("xxxxxx");
		}


		// The equivalent of System.Array.Reverse(System.Array array, int index, int length);.
		public void Reverse()
		{
			int i = 0;
			int j = this.Length - 1;
			while (i < j)
			{
				T temp = this[i];
				this.SetItem(i, this[j]);
				this.SetItem(j, temp);
				i++;
				j--;
			}
		}


		public IEnumerable<Segment<T>> CutUp(int inPieceLength)
		{
			// TO DO: Decide whether one of these alternative names is better:  Split, ChopUp, Partition.
			if (inPieceLength <= 0) throw new Exception("xxxxxx");
			int len = this.Length;
			int currentOffset = 0;
			while (currentOffset < len)
			{
				int generatedSegLen = Math.Min(inPieceLength, len - currentOffset);
				yield return this.GetExtent(currentOffset, generatedSegLen);
				currentOffset += generatedSegLen;
			}
		}

		public static IEnumerable<Segment<T>> operator / (Segment<T> inSegment, int inPieceLength)
		{
			return inSegment.CutUp(inPieceLength);
		}

		// OED defines "splice" as:  "Join or connect (a rope or ropes) by interweaving the strands at the ends."
		// Thus this "CutAndSplice" generates overlapping segments of length inPieceLength.  The length of overlap is specified by the inOverlapLength parameter.
		public IEnumerable<Segment<T>> CutAndSplice(int inPieceLength, int inOverlapLength)
		{
			// TO DO: Decide whether one of these alternative names is better:  SplitAndSplice, PartitionAndSplice.
			if (inPieceLength <= 0 | inOverlapLength < 0 | inOverlapLength >= inPieceLength) throw new Exception("xxxxxx");
			int len = this.Length;
			int currentOffset = 0;
			while (currentOffset < len)
			{
				yield return this.GetExtent(currentOffset, Math.Min(inPieceLength, len - currentOffset));
				currentOffset += inPieceLength - inOverlapLength;
			}
		}

		// If inInvert is true, then it returns the returns the segments that would normally have been skipped.
		public IEnumerable<Segment<T>> Dice(int inPieceLength, int inSkipLength, bool inInvert = false)
		{
			if (inPieceLength <= 0 | inSkipLength < 0) throw new Exception("xxxxxx");
			int len = this.Length;
			int currentOffset = 0;
			if (!inInvert)
			{
				while (currentOffset < len)
				{
					int generatedSegLen = Math.Min(inPieceLength, len - currentOffset);
					yield return this.GetExtent(currentOffset, generatedSegLen);
					currentOffset += generatedSegLen;
					currentOffset += inSkipLength;
				}
			}
			else // invert
			{
				while (currentOffset < len)
				{
					currentOffset += Math.Min(inPieceLength, len - currentOffset);
					if (currentOffset >= len) yield break;
					int generatedSegLen = Math.Min(inSkipLength, len - currentOffset);
					yield return this.GetExtent(currentOffset, generatedSegLen);
					currentOffset += generatedSegLen;
				}
			}
		}

		public IEnumerable<Segment<T>> Split(DlgSegmentSplitter<T> inSplitter)
		{
			if (inSplitter is null) throw new ArgumentNullException();
			int len = this.Length;
			int currentOffset = 0;
			while (currentOffset < len)
			{
				int remainingLen = len - currentOffset;
				int generatedSegLen = inSplitter(this.GetExtentToEnd(currentOffset));
				if (generatedSegLen > remainingLen) generatedSegLen = remainingLen;
				yield return this.GetExtent(currentOffset, generatedSegLen);
				currentOffset += generatedSegLen;
			}
		}


		public static IEnumerable<Segment<T>> Interleave(int inLength, Segment<T> inSeg0, Segment<T> inSeg1)
		{
			throw new NotImplementedException();
		}

		public static IEnumerable<Segment<T>> Interleave(int inLength, params Segment<T>[] inSegments)
		{
			throw new NotImplementedException();
		}




		public bool StartsWith(Segment<T> inOtherSegment, IEqualityComparer<T> inComparer = null)
		{
			if (inComparer is null) inComparer = EqualityComparer<T>.Default;
			throw new NotImplementedException();
		}

		public bool StartsWith(T inValue, IEqualityComparer<T> inComparer = null)
		{
			if (inComparer is null) inComparer = EqualityComparer<T>.Default;
			throw new NotImplementedException();
		}

		public bool EndsWith(Segment<T> inOtherSegment, IEqualityComparer<T> inComparer = null)
		{
			if (inComparer is null) inComparer = EqualityComparer<T>.Default;
			throw new NotImplementedException();
		}

		public bool EndsWith(T inValue, IEqualityComparer<T> inComparer = null)
		{
			if (inComparer is null) inComparer = EqualityComparer<T>.Default;
			throw new NotImplementedException();
		}

		// Returns an Extent covering first occurrence of inDataToFind within this segment, or Extent.Empty if not found.
		public Extent FindFirst(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
		{
			throw new NotImplementedException();
		}

		// Returns an Extent covering last occurrence of inDataToFind within this segment, or null if not found.
		public Extent FindLast(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
		{
			throw new NotImplementedException();
		}

		public Segment<T> FindFirstAndRemove(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
		{
			return this.Remove(this.FindFirst(inDataToFind, inComparer));
		}

		public Segment<T> FindLastAndRemove(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
		{
			return this.Remove(this.FindLast(inDataToFind, inComparer));
		}

		public bool Contains(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
		{
			return this.FindFirst(inDataToFind, inComparer).IsNotEmpty;
		}

		public static bool SequenceEqual(Segment<T> inSegmentA, Segment<T> inSegmentB, IEqualityComparer<T> inComparer = null)
		{
			int lenA = inSegmentA.Length;
			if (lenA != inSegmentB.Length) return false;
			if (inComparer is null) inComparer = EqualityComparer<T>.Default;
			for (int i = 0; i < lenA; i++)
			{
				if (!inComparer.Equals(inSegmentA[i], inSegmentB[i])) return false;
			}
			return true;
		}

		public bool Equals(Segment<T> inOther)	// implements IEquatable<Segment<T>>
		{
			return this.fSourceData == inOther.fSourceData && this.fExtent == inOther.fExtent;
		}
		public override bool Equals(object inOther)
		{
			if (inOther is Segment<T> otherSeg) return this.Equals(otherSeg);
			return false;
		}
		public static bool operator == (Segment<T> inA, Segment<T> inB)
		{
			return inA.Equals(inB);
		}
		public static bool operator != (Segment<T> inA, Segment<T> inB)
		{
			return !inA.Equals(inB);
		}

		public override int GetHashCode()
		{
			throw new NotImplementedException();
		}

		public int CompareTo(Segment<T> inOtherSegment)	// implements IComparable<Segment<T>>
		{
			return Segment<T>.Compare(this, inOtherSegment);
		}

		public static int Compare(Segment<T> inA, Segment<T> inB, IComparer<T> inComparer = null)
		{
			if (inComparer is null) inComparer = Comparer<T>.Default;
			throw new NotImplementedException();
		}
		


		public void CopyTo(Segment<T> inDestinationSegment)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(Extent inSourceExtent, Segment<T> inDestinationSegment)
		{
			this.GetExtent(inSourceExtent).CopyTo(inDestinationSegment);
		}

		public static void Copy(Segment<T> inSourceSegment, Segment<T> inDestinationSegment)
		{
			inSourceSegment.CopyTo(inDestinationSegment);
		}



		/// <summary>Chops off a piece at the start of the segment and returns the remainder.</summary>
		public Segment<T> ChopOffStart(int inPieceLength)
		{
			int oldLen = this.Length;
			if (inPieceLength > oldLen) inPieceLength = oldLen;
			if (inPieceLength < 0) inPieceLength = 0;
			return new Segment<T>(this.fSourceData, this.Offset + inPieceLength, oldLen - inPieceLength);
		}

		/// <summary>Chops off a piece at the end of the segment and returns the remainder.</summary>
		public Segment<T> ChopOffEnd(int inPieceLength)
		{
			int oldLen = this.Length;
			if (inPieceLength > oldLen) inPieceLength = oldLen;
			if (inPieceLength < 0) inPieceLength = 0;
			return new Segment<T>(this.fSourceData, this.Offset, oldLen - inPieceLength);
		}

		// CleaveStart returns the start piece whereas ChopOffStart chops off the start and returns the remainder.
		public Segment<T> CleaveStart(int inPieceLength)
		{
			int oldLen = this.Length;
			if (inPieceLength > oldLen) inPieceLength = oldLen;
			if (inPieceLength < 0) inPieceLength = 0;
			return new Segment<T>(this.fSourceData, this.Offset, inPieceLength);
		}

		public Segment<T> CleaveEnd(int inPieceLength)
		{
			int oldLen = this.Length;
			if (inPieceLength > oldLen) inPieceLength = oldLen;
			if (inPieceLength < 0) inPieceLength = 0;
			return new Segment<T>(this.fSourceData, this.Offset + oldLen - inPieceLength, inPieceLength);
		}

		public void SeverStart(int inPieceLength, out Segment<T> outStartPiece, out Segment<T> outRemainder)
		{
			int oldLen = this.Length;
			if (inPieceLength > oldLen) inPieceLength = oldLen;
			if (inPieceLength < 0) inPieceLength = 0;
			outStartPiece = new Segment<T>(this.fSourceData, this.Offset, inPieceLength);
			outRemainder = new Segment<T>(this.fSourceData, this.Offset + inPieceLength, oldLen - inPieceLength);
		}

		public void SeverEnd(int inPieceLength, out Segment<T> outEndPiece, out Segment<T> outRemainder)
		{
			int oldLen = this.Length;
			if (inPieceLength > oldLen) inPieceLength = oldLen;
			if (inPieceLength < 0) inPieceLength = 0;
			outEndPiece = new Segment<T>(this.fSourceData, this.Offset + oldLen - inPieceLength, inPieceLength);
			outRemainder = new Segment<T>(this.fSourceData, this.Offset, oldLen - inPieceLength);
		}

		public void Sever(Extent inExtent, out Segment<T> outStart, out Segment<T> outMiddle, out Segment<T> outEnd)
		{
			// TO DO: Optimize this implementation.
			outMiddle = this.GetExtent(inExtent);
			outStart = this.GetExtentAtStart(inExtent.Offset);
			outEnd = this.GetExtentAtEnd(this.Length - inExtent.EndOffset);
		}

		public (Segment<T> partA, Segment<T> partB) Bisect(int inOffset)
		{
			// TO DO:  Decide whether "TearAsunder" is a better name for "Bisect".  Alternatively, "RendTheHeavensApart" has the advantage of being even more descriptive.
			int len = this.Length;
			if (inOffset > len) inOffset = len;
			if (inOffset < 0) inOffset = 0;
			// TO DO: Optimize this implementation.
			return (this.GetExtentAtStart(inOffset), this.GetExtent(inOffset, len - inOffset));
		}


		public Segment<T> Remove(Extent inExent)
		{
			return this.Remove(inExent.Offset, inExent.Length);
		}

		public Segment<T> Remove(int inOffset, int inLength)
		{
			int oldOfs = this.Offset;
			int oldLen = this.Length;
			if (!Extent.IsValid(inOffset, inLength, oldLen)) throw new Exception("xxxxxx");
			if (inLength == 0) return this;
			var sd = fSourceData;
			if (sd is null) return Segment<T>.Empty;
			if (inOffset == 0) // if removing at the start
				return new Segment<T>(sd, oldOfs + inLength, oldLen - inLength);
			int removalEndOffset = inOffset + inLength;
			if (removalEndOffset == oldLen) // if removing at/until the end
				return new Segment<T>(sd, oldOfs, oldLen - inLength);
			// removing in the middle.
			int endPieceOfs = oldOfs + removalEndOffset;
			return new Segment<T>(sd.Concatenate(new Segment<T>(sd, oldOfs, inLength: inOffset), new Segment<T>(sd, endPieceOfs, inLength: oldLen - endPieceOfs)));
		}

		public Segment<T> Replace(Extent inExent, Segment<T> inReplacement)
		{
			throw new NotImplementedException();
		}

		public Segment<T> Insert(int inAtOffset, Segment<T> inSourceData)
		{
			return this.Replace(new Extent(inAtOffset, 0), inSourceData);
		}




		public static Segment<T> Concatenate(Segment<T> inSeg0, Segment<T> inSeg1)
		{
			// TO DO:  Decide whether "Join" is a better name than "Concatenate".  See also operator +.
			ISegmentable<T> data0 = inSeg0.fSourceData;
			if (data0 is null) return inSeg1;
			return new Segment<T>(data0.Concatenate(inSeg0, inSeg1));
		}

		public static Segment<T> Concatenate(Segment<T> inSeg0, Segment<T> inSeg1, Segment<T> inSeg2)
		{
			throw new NotImplementedException();
		}

		public static Segment<T> Concatenate(Segment<T> inSeg0, Segment<T> inSeg1, Segment<T> inSeg2, Segment<T> inSeg3)
		{
			throw new NotImplementedException();
		}

		public static Segment<T> Concatenate(params Segment<T>[] inSegments)
		{
			return Concatenate((IEnumerable<Segment<T>>)inSegments);
		}

		public static Segment<T> Concatenate(IEnumerable<Segment<T>> inSegments)
		{
			if (inSegments is null) return Segment<T>.Empty;
			throw new NotImplementedException();
		}

		public static Segment<T> operator + (Segment<T> inSegmentA, Segment<T> inSegmentB)
		{
			return Segment<T>.Concatenate(inSegmentA, inSegmentB);
		}

		public static Segment<T> operator + (Segment<T> inSegmentA, IEnumerable<Segment<T>> inSegments)
		{
			if (inSegments is null) return inSegmentA;
			return Segment<T>.Concatenate(System.Linq.Enumerable.Prepend(inSegments, inSegmentA));
		}

		public static Segment<T> operator + (IEnumerable<Segment<T>> inSegments, Segment<T> inSegmentB)
		{
			if (inSegments is null) return inSegmentB;
			return Segment<T>.Concatenate(System.Linq.Enumerable.Append(inSegments, inSegmentB));
		}


		public TWhole MakeWhole<TWhole>() where TWhole : ISegmentable<T>
		{
			// TO DO:  Try to think of a better name than "MakeWhole".
			var sd = fSourceData;
			if (sd is null) throw new NullReferenceException();
			int ofs = this.Offset;
			int len = this.Length;
			if (!Extent.IsValid(ofs, len, sd.Length)) throw new Exception("xxxxxx");
			return (TWhole) sd.NewFromExtent(ofs, len);
		}


		public IEnumerable<FactionalizedSegment<T,TFaction>> Factionalize<TFaction>(DlgFactionalizer<T,TFaction> inFactionalizer)
		{
			throw new NotImplementedException();
		}


		public IEnumerable<T> GetMatchingElements(IElementMatcher<T> inMatcher, bool inInvert = false)
		{
			if (inMatcher is null) throw new ArgumentNullException();
			foreach (T element in this)
			{
				if (inMatcher.IsMatch(element) ^ inInvert) yield return element;
			}
		}

		public IEnumerable<T> GetMatchingElements(Func<T,bool> inMatcher, bool inInvert = false)
		{
			if (inMatcher is null) throw new ArgumentNullException();
			foreach (T element in this)
			{
				if (inMatcher(element) ^ inInvert) yield return element;
			}
		}

		public IEnumerable<T> GetNonMatchingElements(IElementMatcher<T> inMatcher)
		{
			return this.GetMatchingElements(inMatcher, inInvert: true);
		}

		public IEnumerable<T> GetNonMatchingElements(Func<T,bool> inMatcher)
		{
			return this.GetMatchingElements(inMatcher, inInvert: true);
		}

		public Int64 CountMatchingElements(IElementMatcher<T> inMatcher, bool inInvert = false)
		{
			if (inMatcher is null) throw new ArgumentNullException();
			Int64 cnt = 0;
			foreach (T element in this)
			{
				if (inMatcher.IsMatch(element) ^ inInvert)
				{
					checked { cnt++; }
				}
			}
			return cnt;
		}

		public Int64 CountMatchingElements(Func<T,bool> inMatcher, bool inInvert = false)
		{
			if (inMatcher is null) throw new ArgumentNullException();
			Int64 cnt = 0;
			foreach (T element in this)
			{
				if (inMatcher(element) ^ inInvert)
				{
					checked { cnt++; }
				}
			}
			return cnt;
		}

		public Int64 CountNonMatchingElements(IElementMatcher<T> inMatcher, bool inInvert = false)
		{
			return this.CountMatchingElements(inMatcher, inInvert: true);
		}

		public Int64 CountNonMatchingElements(Func<T,bool> inMatcher, bool inInvert = false)
		{
			return this.CountMatchingElements(inMatcher, inInvert: true);
		}


		public IEnumerator<T> GetEnumerator()
		{
			throw new NotImplementedException();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public IEnumerable<T> IterateExtent(Extent inExtent)
		{
			return this.GetExtent(inExtent);
		}

		public IEnumerable<T> IterateExtent(int inOffset, int inLength)
		{
			return this.GetExtent(inOffset, inLength);
		}

		public IEnumerable<T> IterateRange(int inStartOffset, int inEndOffset)
		{
			return this.GetRange(inStartOffset, inEndOffset);
		}

		public IEnumerable<T> IterateToEnd(int inStartOffset)
		{
			return this.GetExtentToEnd(inStartOffset);
		}

		public IEnumerable<T> IterateBackwards()
		{
			return this.InReverse;
		}

		public IEnumerable<T> IterateBackwards(Extent inExtent)
		{
			return this.GetExtent(inExtent).InReverse;
		}

		public void Sort(IComparer<T> inComparer = null)
		{
			if (inComparer is null) inComparer = Comparer<T>.Default;
			throw new NotImplementedException();
		}

		// Uses the binary search algorithm to find an element that equals inValue.
		public int? SortedSearch(T inValue, IComparer<T> inComparer = null)
		{
			if (inComparer is null) inComparer = Comparer<T>.Default;
			throw new NotImplementedException();
		}



		/* ======================================================================================================================= */

		public struct Builder
		{
			//private readonly ISegmentableBuilder<T> fInternalBuilder;

			public int Length
			{
				get { throw new NotImplementedException(); }
			}
			public int Capacity
			{
				get { throw new NotImplementedException(); }
			}

			public void Truncate(int inMaxLength)
			{
				if (inMaxLength < 0) inMaxLength = 0;
				if (inMaxLength >= this.Length) return;
				throw new NotImplementedException();
			}

			public T this[int inOrdinal]
			{
				get
				{
					throw new NotImplementedException();
				}
				set
				{
					throw new NotImplementedException();
				}
			}

			public void Append(Segment<T> inSource)
			{
				throw new NotImplementedException();
			}

			public void AppendItem(T inElement)
			{
				throw new NotImplementedException();
			}

			// returns count of items appended (count of new items).
			public int AppendItems(IEnumerable<T> inItems)
			{
				return this.InsertItems(this.Length, inItems);
			}

			public void Remove(Extent inExent)
			{
				this.Remove(inExent.Offset, inExent.Length);
			}

			public void Remove(int inOffset, int inLength)
			{
				throw new NotImplementedException();
			}

			public void RemoveAll()
			{
				this.Remove(0, this.Length);
			}

			public void Replace(Extent inExent, Segment<T> inReplacement)
			{
				throw new NotImplementedException();
			}

			public void Insert(int inAtOffset, Segment<T> inSourceData)
			{
				this.Replace(new Extent(inAtOffset, 0), inSourceData);
			}

			public void InsertItem(int inAtOffset, T inElement)
			{
				throw new NotImplementedException();
			}

			// returns count of items appended (count of new items).
			public int InsertItems(int inAtOffset, IEnumerable<T> inItems)
			{
				throw new NotImplementedException();
			}

			public void CopyTo(Segment<T> inDestinationSegment)
			{
				this.CopyTo(new Extent(0, this.Length), inDestinationSegment);
			}

			public void CopyTo(Extent inSourceExtent, Segment<T> inDestinationSegment)
			{
				throw new NotImplementedException();
			}

			public Extent FindFirst(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
			{
				throw new NotImplementedException();
			}

			// Returns an Extent covering last occurrence of inDataToFind within this segment, or null if not found.
			public Extent FindLast(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
			{
				throw new NotImplementedException();
			}

			public bool Contains(Segment<T> inDataToFind, IEqualityComparer<T> inComparer = null)
			{
				return this.FindFirst(inDataToFind, inComparer).IsNotEmpty;
			}


		} // struct Builder

	} // struct Segment<T>


	/* ======================================================================================================================= */
	/* ======================================================================================================================= */
	/* ======================================================================================================================= */


	// TO DO:  Decide whether to keep SegmentInReverse<T> versus delete it and use Segment<T>.GetItemReverse instead.  See also System.Array.Reverse(System.Array array, int index, int length);
	public readonly struct SegmentInReverse<T> : IReadOnlyList<T> //, IEquatable<Segment<T>>, IComparable<Segment<T>>
	{
		private readonly Segment<T> fSegment;

		public SegmentInReverse(Segment<T> inSegment)
		{
			fSegment = inSegment;
		}

		public T this[int inOrdinal]
		{
			get
			{
				return fSegment.GetItemReverse(inOrdinal);
			}
			set
			{
				fSegment.SetItemReverse(inOrdinal, value);
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			Segment<T> seg = fSegment;	// Copy field to local variable to prevent it being modified during iteration.
			int i = seg.Length;
			while (i > 0)
			{
				unchecked { i--; }
				yield return seg[i];
			}
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public int Length { get { return fSegment.Length; } }
		int IReadOnlyCollection<T>.Count { get { return fSegment.Length; } }

	} // struct SegmentInReverse<T>


	/* ======================================================================================================================= */
	/* ======================================================================================================================= */
	/* ======================================================================================================================= */

	/*
	A class implements ISegmentable<T> in order to be compatible/usable with Segment<T>.
	Classes and structs that would implement ISegmentable<T> include:
		* System.Array
		* System.String
		* System.Collections.Generic.List<T>
		* System.Collections.Generic.Stack<T>
		* System.Collections.Generic.Queue<T>
		* System.Collections.Generic.SortedList<TKey, TValue>
		* System.Collections.Immutable.ImmutableArray<T>
		* System.Collections.Immutable.ImmutableList<T>
		* System.Collections.Immutable.ImmutableStack<T>
		* System.Collections.Immutable.ImmutableQueue<T>
		* System.Span<T>
		* System.ReadOnlySpan<T>
	*/
	public interface ISegmentable<T>
	{
		int Length { get; }
		T this[int inOrdinal] { get; set; }
		ISegmentable<T> Concatenate(Segment<T> inSeg0, Segment<T> inSeg1);
		ISegmentable<T> NewFromExtent(int inOffset, int inLength);

	} // interface ISegmentable<T>


	/* ======================================================================================================================= */
	/* ======================================================================================================================= */
	/* ======================================================================================================================= */


	// Note implementations of IElementMatcher<T> are normally immutable reusable classes.  Singleton instances are used when suitable.
	public interface IElementMatcher<T>
	{
		bool IsMatch(T inElement);

	} // interface IElementMatcher<T>


	/* ======================================================================================================================= */
	/* ======================================================================================================================= */
	/* ======================================================================================================================= */

	public static class CharMatchers
	{
		/// <summary>Never matches any character.</summary>
		public static IElementMatcher<char> NoChars
		{
			get { throw new NotImplementedException(); }
		}

		/// <summary>Always matches any and all characters.</summary>
		public static IElementMatcher<char> AllChars
		{
			get { throw new NotImplementedException(); }
		}

		/// <summary>Matches characters categorized as a Unicode "letter", in either lowercase or uppercase.  Excludes numeric digits.</summary>
		public static IElementMatcher<char> Alphabetic
		{
			get { throw new NotImplementedException(); }
		}

		/// <summary>Matches characters categorized as a Unicode "letter" (either lowercase or uppercase) or a numeric digit.</summary>
		public static IElementMatcher<char> LetterOrDigit
		{
			get { throw new NotImplementedException(); }
		}

		/// <summary>An uppercase alphabetic letter.  Unicode designation "Lu" (letter, uppercase).</summary>
		public static IElementMatcher<char> UppercaseLetter
		{
			get { throw new NotImplementedException(); }
		}
		/// <summary>A lowercase alphabetic letter.  Unicode designation "Ll" (letter, lowercase).</summary>
		public static IElementMatcher<char> LowercaseLetter
		{
			get { throw new NotImplementedException(); }
		}
		
		public static IElementMatcher<char> DecimalDigit
		{
			get { throw new NotImplementedException(); }
		}

		public static IElementMatcher<char> HexadecimalDigit
		{
			get { throw new NotImplementedException(); }
		}

		public static IElementMatcher<char> WhiteSpace
		{
			get { throw new NotImplementedException(); }
		}

		public static IElementMatcher<char> HorizontalWhiteSpace
		{
			get { throw new NotImplementedException(); }
		}

		public static IElementMatcher<char> VerticalWhiteSpace
		{
			get { throw new NotImplementedException(); }
		}

	} // static class CharMatchers


	/* ======================================================================================================================= */
	/* ======================================================================================================================= */
	/* ======================================================================================================================= */


	public delegate int DlgSegmentSplitter<TElement>(Segment<TElement> inRemaining);


	// TFaction is often an enumeration type.
	public readonly struct FactionalizedSegment<TElement, TFaction> : IEquatable<FactionalizedSegment<TElement, TFaction>>
	{
		private readonly Segment<TElement> fSegment;
		private readonly TFaction fFaction;

		public FactionalizedSegment(Segment<TElement> inSegment, TFaction inFaction)
		{
			fSegment = inSegment;
			fFaction = inFaction;
		}

		public Segment<TElement> Segment { get { return fSegment; } }
		public TFaction Faction { get { return fFaction; } }

		public int Length { get { return fSegment.Length; } }

		public bool Equals(FactionalizedSegment<TElement, TFaction> inOther)
		{
			return this.fSegment.Equals(inOther.fSegment) && EqualityComparer<TFaction>.Default.Equals(this.fFaction, inOther.fFaction);
		}

		public override bool Equals(object inOther)
		{
			if (inOther is FactionalizedSegment<TElement, TFaction> otherFSeg) return this.Equals(otherFSeg);
			return false;
		}
		public static bool operator == (FactionalizedSegment<TElement, TFaction> inA, FactionalizedSegment<TElement, TFaction> inB)
		{
			return inA.Equals(inB);
		}
		public static bool operator != (FactionalizedSegment<TElement, TFaction> inA, FactionalizedSegment<TElement, TFaction> inB)
		{
			return !inA.Equals(inB);
		}

		public override int GetHashCode()
		{
			throw new NotImplementedException();
		}

	} // struct FactionalizedSegment


	// This delegate inspects one or more elements at the start of inRemaining and returns the faction that they belong to, and sets outSegmentLength to the length of the factionalized segment.
	// If the delegate sets outSegmentLength to a non-null length, then FactionalizedSegment.Length becomes outSegmentLength and adjacent factions of same kind are not merged.
	// If outSegmentLength is set to null, then it is interpreted as if it was set to 1 except with the difference that multiple adjacent factions of the same kind are merged into a single FactionalizedSegment.
	// Sometimes it is necessary for the delegate to look ahead, meaning it can read/inspect more elements of inRemaining than it sets outSegmentLength to.
	public delegate TFaction DlgFactionalizer<TElement, TFaction>(Segment<TElement> inRemaining, out int? outSegmentLength);



	/* ======================================================================================================================= */

} // namespace
