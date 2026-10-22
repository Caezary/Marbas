namespace Marbas.Guide

type GuideFault =
    | Absent of ChapterName
    | Refused of ChapterName * Refusal
