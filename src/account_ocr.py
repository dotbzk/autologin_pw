from dataclasses import dataclass
from difflib import SequenceMatcher
from itertools import permutations
import unicodedata


@dataclass(frozen=True)
class OcrLine:
    text: str
    confidence: float
    box: tuple[tuple[float, float], ...]

    @property
    def center(self):
        xs = [point[0] for point in self.box]
        ys = [point[1] for point in self.box]
        return (sum(xs) / len(xs), sum(ys) / len(ys))


def normalize_account_name(value):
    """Normalize OCR text while ignoring separators such as underscores."""
    normalized = unicodedata.normalize("NFKC", value).casefold()
    return "".join(char for char in normalized if char.isalnum())


def find_matching_line(target, lines, min_confidence=0.45, fuzzy_threshold=0.86):
    target_normalized = normalize_account_name(target)
    if not target_normalized:
        return None

    candidates = []
    for line in lines:
        if line.confidence < min_confidence:
            continue

        recognized = normalize_account_name(line.text)
        if not recognized:
            continue

        if recognized == target_normalized:
            return line

        # A wrong first character can point to a different account entirely.
        # For example, OCR may crop "luk_kapela" to "ukkapela", which must
        # never be accepted as "mk_kapela" by the fuzzy matcher.
        if recognized[0] != target_normalized[0]:
            continue

        similarity = SequenceMatcher(None, target_normalized, recognized).ratio()
        candidates.append((similarity, line.confidence, line))

    if not candidates:
        return None

    similarity, _, line = max(candidates, key=lambda item: (item[0], item[1]))
    return line if similarity >= fuzzy_threshold else None


def find_exact_combined_match(target, lines, min_confidence=0.45, max_lines=3):
    """Find an exact account name split by OCR into adjacent text fragments."""
    target_normalized = normalize_account_name(target)
    eligible = [line for line in lines if line.confidence >= min_confidence]

    for size in range(2, min(max_lines, len(eligible)) + 1):
        for group in permutations(eligible, size):
            combined = "".join(line.text for line in group)
            if normalize_account_name(combined) == target_normalized:
                return list(group)

    return None


class AccountTextRecognizer:
    """Lazy RapidOCR adapter so importing the application stays inexpensive."""

    def __init__(self, engine=None):
        self._engine = engine

    def _get_engine(self):
        if self._engine is None:
            try:
                from importlib.resources import files
                from rapidocr import RapidOCR
            except ImportError as exc:
                raise RuntimeError(
                    "OCR dependency is missing. Install src/requirements.txt."
                ) from exc

            model_dir = files("rapidocr").joinpath("models")
            self._engine = RapidOCR(
                params={
                    "Det.model_path": str(model_dir.joinpath("ch_PP-OCRv4_det_infer.onnx")),
                    "Rec.model_path": str(model_dir.joinpath("ch_PP-OCRv4_rec_infer.onnx")),
                    "Cls.model_path": str(model_dir.joinpath("ch_ppocr_mobile_v2.0_cls_infer.onnx")),
                }
            )

        return self._engine

    def recognize(self, image):
        import numpy as np

        result = self._get_engine()(np.asarray(image.convert("RGB")))
        texts = getattr(result, "txts", None)
        scores = getattr(result, "scores", None)
        boxes = getattr(result, "boxes", None)

        if texts is None or scores is None or boxes is None:
            return []

        lines = []
        for text, confidence, box in zip(texts, scores, boxes):
            points = tuple((float(point[0]), float(point[1])) for point in box)
            if not points:
                continue
            lines.append(OcrLine(str(text), float(confidence), points))

        return lines
