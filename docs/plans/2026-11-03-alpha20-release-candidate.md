English | [中文](2026-11-03-alpha20-release-candidate.cn.md)

# Alpha.20 formal candidate preparation — #134

Status: IN_PROGRESS. On2026-11-03 the user authorized preparing the formal
alpha.20 candidate and local pre-release checks. This covers P20-601 and the
local preparation/verification portion of P20-602; publication/registry writes,
tags, merges, Gitee, images and beta remain outside this request. Existing bounded
experimental acceptance under163/172 and its original-ID dispositions remain.

1. Freeze one coherent source bump to1.0.0-alpha.20, including runtime version
   defaults, internal exact dependencies, daemon overlays, changelogs and a
   prospective public NPS overlay. Python's packager normalizes the same version
   to1.0.0a20. Protocol document versions keep their independent existing matrix.
2. Materialize all nine corresponding release projections from clean committed
   Git blobs/modes into fresh isolated output. Existing SDK ownership stays;
   add an offline suite projector because legacy tool sync scripts create tags
   even with DRY_RUN. No legacy publisher is executed.
3. Apply reviewed owned scopes to dedicated candidate worktrees/branches,
   preserving unrelated editorial files. NPS/version.yaml is finalized last as
   the actual suite oracle; its status remainscandidate/released_atnull.
   Fetch/read back every selected upstream and require clean matching HEADs.
4. Run current source/version/spec/SDK drift checks and actual package/regression
   checks locally. Build eleven NuGet+symbol pairs from the real public SDK Git
   commit; require repository/commit and SourceLink to identify public inputs.
   Build npm, wheel/sdist, Java and eight Rust packages, and verify Go module
   contents/compiled version. Original closed experimental packets remain
   immutable; historical package-consumer receipts cannot qualify changed
   manifests. Record exact commands/exits, inventories, hashes and limits.
5. Bind the actual candidate version and all ten selected repository commits
   with the canonical pre-release receipt helper, after coherence checks pass.
   Capture checks separately from candidate identity. Missing publish authority,
   signing or other readiness gates remain explicit blockers; never reinterpret
   a local build or old alpha.19 receipt as permission or ready-to-tag.

Acceptance: coherent20 source/projections; no unexplained owned tree drift;
actual candidate packages and meaningful consumer/compatibility/security results;
source/commit/version-bound receipts, EN/CN notes and a concrete release handoff.
Required checks retain real failures. Current safety and original beta/full
production dispositions are not waived. LocalCI only: no Actions or new model
requests. Public source candidates may use reviewed preparation branches/PRs;
publication remains a separate explicit decision.
