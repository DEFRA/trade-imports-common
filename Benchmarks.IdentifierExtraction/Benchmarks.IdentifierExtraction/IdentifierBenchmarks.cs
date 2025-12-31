using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Error, Column.StdDev)]
public partial class IdentifierBenchmarks
{
	// Parameterize input “shapes” to understand best/worst cases.
	public enum InputShape
	{
		One,
		Two,
	}

	[Params(
		InputShape.One,
		InputShape.Two)]
	public InputShape Shape { get; set; }

	// Optional toggle if you want to approximate “validation” overhead
	// (e.g., null/empty checks, prefix checks, etc.)
	[Params(false, true)]
	public bool IncludeValidationLikeChecks { get; set; }

	private string? _input;

	[GlobalSetup]
	public void Setup()
	{
		_input = Shape switch
		{
			InputShape.One => "CHEDA.GB.2025.123456",
			InputShape.Two => "GBCHDN.2025.1234567R",
			
		};
	}

	// --------------------------
	// Benchmarked implementations
	// --------------------------

	[Benchmark(Baseline = true), BenchmarkCategory("Extract")]
	public string Span_FilterLast7Digits()
		=> GetLast7Digits(_input);

	[Benchmark, BenchmarkCategory("Extract")]
	public string Regex_Trailing7DigitsOptionalSuffix()
		=> GetTrailingIdentifierRegex(_input, IncludeValidationLikeChecks);

	// --------------------------
	// Implementation under test A
	// --------------------------

	public static string GetLast7Digits(string? input)
	{
		if (string.IsNullOrEmpty(input))
			return input ?? string.Empty;

		var span = input.AsSpan();

		// Stackalloc buffer of input length (note: large strings increase stack usage).
		Span<char> digits = stackalloc char[span.Length];
		var count = 0;

		foreach (var c in span)
		{
			if (char.IsDigit(c))
				digits[count++] = c;
		}

		if (count < 7)
			return string.Empty;

		return digits.Slice(count - 7, 7).ToString(); // allocates result
	}

	// --------------------------
	// Implementation under test B
	// --------------------------

	public static string GetTrailingIdentifierRegex(string? input, bool includeValidationLikeChecks)
	{
		if (string.IsNullOrEmpty(input))
			return string.Empty;

		if (includeValidationLikeChecks)
		{
			// Example “validation-like” gates (customize to match your IsValid)
			// Keep it minimal and deterministic for benchmarking.
			if (input.Length < 7)
				return string.Empty;
		}

		// GeneratedRegex caches a singleton Regex instance behind the partial method.
		var match = ChedReferenceRegexes.DocumentReferenceIdentifier().Match(input);
		return match.Success ? match.Value : string.Empty;
	}

	public static partial class ChedReferenceRegexes
	{
		// Matches: 7 digits at end, optionally followed by v or r (case-insensitive).
		[GeneratedRegex("\\d{7}(v|r)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
		public static partial Regex DocumentReferenceIdentifier();
	}

	// --------------------------
	// Input generators
	// --------------------------

	private static string MakeLongMixed(bool endingHas7Digits)
	{
		var core = "CHED-IMPORTS-REF-ABC-2025-12-31-";
		var mid = "X9Y8Z7W6V5U4T3S2R1-";
		var tail = endingHas7Digits ? "1234567" : "123456X";
		return core + mid + tail;
	}

	private static string MakeVeryLongMostlyDigits(bool endingHas7Digits)
	{
		// 4k-ish characters, mostly digits with separators.
		// This stresses stackalloc for the Span version and scanning for regex.
		var len = 4096;
		var chars = new char[len];
		for (int i = 0; i < len; i++)
		{
			chars[i] = (i % 11 == 0) ? '-' : (char)('0' + (i % 10));
		}

		// Ensure ending is controlled
		var tail = endingHas7Digits ? "1234567" : "123456X";
		tail.AsSpan().CopyTo(chars.AsSpan(len - tail.Length));

		return new string(chars);
	}
}