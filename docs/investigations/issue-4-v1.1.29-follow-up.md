# Issue #4: report after v1.1.29

Date: 2026-09-09. [Issue discussion](https://github.com/Eliminater74/MetaQuestTrayTool/issues/4).

The tester confirmed Meta Quest Link on September 8 and reported that bitrate and other Quest Link values remained unchanged after reopening ODT and restarting OVRService. On September 9 they reported both the unchanged settings and missing Exit hotkey again with v1.1.29. These reports do not establish which apply branch ran. Do not assume the tester uses Steam Link or Virtual Desktop.

## Confirmed hotkey layout defect

ExitApp already exists in the action catalog, the configuration dropdown, and command dispatch. It intentionally has no default chord. However, HotKeysWindow was a fixed 760 by 560 window with an unbounded vertical StackPanel. The binding ListView had only a minimum height, so the list could grow with its items. Add binding and the Action/Record editor followed it without an outer scrollbar. Catalog tests could pass while the user could not reach the editor.

The window now resizes and scrolls, with a bounded list that scrolls independently. A visible hint explains how to assign Exit. Existing bindings and default chords are preserved.

Two WPF layout regressions load the production XAML without application startup, event handlers, or theme resources. They measure small/default-list and larger/long-list cases and check the Record button can be brought into the viewport. Against the original layout, both fail: the button starts at about y=627 in a 390-high viewport, and y=1613 in a 630-high viewport. Both pass with the repair. This verifies layout reachability, not physical global hotkey execution or the tester's precise screen configuration.

## ODT settings remain unresolved

Traced Quest Link selection handlers through PersistApply, session capability checks, ApplyMetaLinkSettings, and LinkSettingsService registry write/read-back verification. The prior local ODT interoperability evidence remains documented in issue-4-link-registry.md; it does not prove success on the tester's installation. No new registry mapping, session classifier, or runtime-cache defect was established in this follow-up.

One concrete diagnostic gap was fixed: the page's early capability skip previously returned after setting a generic status string, without logging it. It now names the detected streamer in the status and logs the skip, detected session kind/active state, and requested Link settings. Successful writes and verification errors continue through the existing application logging. This change is diagnostic only and does not resolve the reported ODT failure or bypass session guards.

## Validation

Locked win-x64 restore succeeded; Release build completed with zero warnings/errors; all 98 tests passed, including the two new layout cases. The original-layout countercheck failed both new cases as expected. Diff whitespace validation passed. No physical headset test, registry mutation, OVRService restart, installer, version bump, commit, push, release, or issue comment was performed.
