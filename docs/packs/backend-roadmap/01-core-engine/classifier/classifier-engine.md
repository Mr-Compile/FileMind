# Classifier Engine
Pipeline: normalize -> existing trusted rules/patterns -> filename -> type/MIME -> metadata -> local content -> context -> confidence -> optional AI fallback.

Context beats generic type when evidence is strong. Example: `IT421_Lecture_5.mp4` belongs to School/IT421/Lectures rather than generic Videos.

Each classifier emits evidence, score contribution, and explanation. The aggregator produces ClassificationResult.
