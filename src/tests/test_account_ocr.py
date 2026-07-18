import unittest

from src.account_ocr import (
    OcrLine,
    find_exact_combined_match,
    find_matching_line,
    get_lines_center,
    normalize_account_name,
    normalize_account_tokens,
)


def line(text, confidence=0.95, left=10, top=20):
    return OcrLine(
        text,
        confidence,
        ((left, top), (left + 100, top), (left + 100, top + 20), (left, top + 20)),
    )


class NormalizeAccountNameTests(unittest.TestCase):
    def test_ignores_case_and_separators(self):
        self.assertEqual(normalize_account_name(" Luk_Fenrir "), "lukfenrir")
        self.assertEqual(normalize_account_name("luk-fenrir"), "lukfenrir")

    def test_extracts_account_tokens(self):
        self.assertEqual(
            normalize_account_tokens(" x3_mist-fenrir "),
            ["x3", "mist", "fenrir"],
        )


class FindMatchingLineTests(unittest.TestCase):
    def test_matches_when_ocr_drops_underscore(self):
        expected = line("luk fenrir")
        self.assertIs(find_matching_line("luk_fenrir", [expected]), expected)

    def test_accepts_small_ocr_error_for_long_name(self):
        expected = line("tank_fenriг")
        self.assertIs(find_matching_line("tank_fenrir", [expected]), expected)

    def test_rejects_unrelated_or_low_confidence_text(self):
        self.assertIsNone(find_matching_line("luk_fenrir", [line("settings")]))
        self.assertIsNone(find_matching_line("luk_fenrir", [line("luk_fenrir", 0.2)]))

    def test_does_not_match_cropped_luk_as_mk(self):
        self.assertIsNone(find_matching_line("mk_kapela", [line("ukkapela")]))
        self.assertIsNone(find_matching_line("mk_kapela", [line("lukkapela")]))

    def test_requires_exact_numbered_prefix_for_fuzzy_match(self):
        self.assertIsNone(find_matching_line("x3_mist_fenrir", [line("v3_mist_fenrir")]))
        self.assertIsNone(find_matching_line("x3_mist_fenrir", [line("x5_mist_fenrir")]))

    def test_accepts_ocr_error_after_exact_numbered_prefix(self):
        expected = line("x3_mist_fenriг")
        self.assertIs(find_matching_line("x3_mist_fenrir", [expected]), expected)

    def test_returns_box_center(self):
        self.assertEqual(line("luk_fenrir").center, (60, 30))


class FindExactCombinedMatchTests(unittest.TestCase):
    def test_matches_account_split_into_two_ocr_lines(self):
        parts = [line("kosa", left=10), line("_kapela", left=115)]
        self.assertEqual(find_exact_combined_match("kosa_kapela", parts), parts)

    def test_matches_fragments_returned_in_reverse_ocr_order(self):
        prefix = line("War", left=10)
        suffix = line("_kapela", left=115)

        self.assertEqual(
            find_exact_combined_match("war_kapela", [suffix, prefix]),
            [prefix, suffix],
        )

    def test_does_not_fuzzy_match_combined_fragments(self):
        parts = [line("koca", left=10), line("_kapela", left=115)]
        self.assertIsNone(find_exact_combined_match("kosa_kapela", parts))

    def test_ignores_low_confidence_fragments(self):
        parts = [line("kosa", left=10), line("_kapela", 0.2, left=115)]
        self.assertIsNone(find_exact_combined_match("kosa_kapela", parts))

    def test_does_not_join_fragments_from_different_rows(self):
        parts = [line("kosa", left=10, top=20), line("_kapela", left=115, top=80)]
        self.assertIsNone(find_exact_combined_match("kosa_kapela", parts))

    def test_returns_combined_center(self):
        parts = [line("kosa", left=10), line("_kapela", left=115)]
        self.assertEqual(get_lines_center(parts), (112.5, 30))


if __name__ == "__main__":
    unittest.main()
