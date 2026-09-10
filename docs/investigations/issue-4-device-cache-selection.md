# Issue #4: active Meta Link headset hidden by another cache entry

Date: 2026-09-10. Follow-up to [the v1.1.29 report](issue-4-v1.1.29-follow-up.md).

The tester again confirmed Meta Link, not the Steam Link app. Investigation therefore traced the application's transport classification and later registry writers. The reporter's particular environment remains unverified.

## Reproduced defect

`ReadHeadsetCache` chose an entry when it was newer **or** active while the previous entry was inactive. A newer idle entry appearing after a live headset could therefore replace it. A weak connected entry could also hide a live entry regardless of array order because both qualified under the broader active-session predicate.

For example, a live headset with `connectionState=connected`, `operationalState=operable`, and `lastSeenAt=100` was replaced by a later idle headset with `lastSeenAt=200`. When SteamVR was running, `ProbeCore` evaluated only the selected idle entry, found no strong Meta signal, and took the SteamVR/non-Meta branch. Its capability guard then skipped the Link registry writes. SteamVR over Meta Link can consequently be treated as Steam Link without the user actually using Steam Link.

## Repair

Select the headset with the strongest existing evidence first: live Meta stream evidence, then weaker active-session evidence, then idle evidence. Compare `lastSeenAt` only within the same evidence class. Parsing now has an internal test seam; it still uses the same shared file read and exception handling.

No registry names, values, hives, default-reset behavior, or transport evidence predicates changed. Actual non-Meta capability guards remain in place. This repairs selection among cached headsets; it does not assume every running SteamVR process uses Meta Link.

## Verification and limits

Eight regression cases cover both array orders for live-versus-newer-idle and live-versus-newer-weak entries, recency within equal evidence classes, stale connected/inoperable state, non-headset records, and an empty cache. Three cases failed against the original selection algorithm, selecting the wrong headset. All pass after the repair. The tests also exercise the exact strong-Meta predicate used before the SteamVR fallback.

Locked win-x64 restore succeeded. Release build: zero warnings/errors. Full suite: 106/106 passed. Diff whitespace checks passed. Tests use synthetic device-cache records and do not write the real registry or device cache.

The local device cache inspected during this follow-up contained one disconnected/inoperable headset, so it cannot reproduce the multi-headset condition physically. The tester's cache has not been supplied. This is a proven application defect and a possible cause of issue #4, not proof that the tester's failure is resolved.

## Other registry writers reviewed

Startup and optional ADB-connect baseline application write saved global Link settings when enabled; the baseline skips while a personal profile is active. A profile with Link overrides overlays the saved global settings. Profile exit or cancelled launch restores saved globals. These operations can replace external ODT edits, but that is distinct from ODT reopening resetting its own registry values. No additional cause of the tester's report was established in those paths during this follow-up.
