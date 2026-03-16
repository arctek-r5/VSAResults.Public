# VsaResults Subtree Extraction

This document explains how to publish the VsaResults family from this monorepo into its own GitHub repository using `git subtree`.

## Goal

Extract the VsaResults code into a standalone repository while preserving history for the extracted subtree.

## Current Layout

The VsaResults family is now consolidated under one subtree-friendly root:

```text
src/ThirdParty/VsaResults/
  LICENSE
  README.md
  SUBTREE-EXTRACTION.md
  Directory.Build.props
  Directory.Packages.props
  global.json
  VsaResults.slnx
  src/
    VsaResults/
    VsaResults.Analyzers/
    VsaResults.AspNetCore/
    VsaResults.Features/
    VsaResults.Messaging/
    VsaResults.Messaging.RabbitMq/
    VsaResults.Messaging.Kafka/
    VsaResults.Messaging.AzureServiceBus/
    VsaResults.Messaging.PostgreSql/
    VsaResults.Observability/
```

This layout is what `git subtree split --prefix=src/ThirdParty/VsaResults` expects.

## Create the Split Branch

From the monorepo root:

```bash
git subtree split --prefix=src/ThirdParty/VsaResults -b vsaresults-split
```

This creates a local branch named `vsaresults-split` containing only the history for that subtree.

## Push to a Standalone GitHub Repo

Add the destination repository as a remote:

```bash
git remote add vsaresults git@github.com:<org>/<repo>.git
```

Push the split branch to the standalone repository:

```bash
git push -u vsaresults vsaresults-split:main
```

If the target repository already exists and uses a different default branch, replace `main` with that branch name.

## Change the Destination Repo Later

The subtree split branch is independent of the remote you push to. You can repoint it to a different repository at any time.

Replace the remote:

```bash
git remote remove vsaresults
git remote add vsaresults git@github.com:<org>/<different-repo>.git
git push -u vsaresults vsaresults-split:main
```

Or simply update the remote URL:

```bash
git remote set-url vsaresults git@github.com:<org>/<different-repo>.git
git push -u vsaresults vsaresults-split:main
```

## Ongoing Sync

If you continue developing in the monorepo and want to republish changes:

```bash
git subtree split --prefix=src/ThirdParty/VsaResults -b vsaresults-split
git push vsaresults vsaresults-split:main
```

If you also want to pull changes back from the standalone repo, use standard subtree workflows from the monorepo root:

```bash
git subtree pull --prefix=src/ThirdParty/VsaResults vsaresults main --squash
```

Only do this once the standalone repo has a stable structure that still matches the subtree layout in this monorepo.

## Recommended Prep Before First Push

1. Consolidate all VsaResults projects under one parent directory.
2. Verify `dotnet build` from that consolidated root.
3. Verify `dotnet pack` for the published packages.
4. Add a standalone-friendly solution file and any repo-local `Directory.Build.props` needed by the extracted repo.
5. Split and push only after the extracted tree builds without relying on unrelated monorepo files.

## Verification Checklist

- The subtree prefix contains every VsaResults project that should live in the standalone repo.
- All relative `ProjectReference` paths resolve within the subtree.
- README and LICENSE files are packed correctly.
- The extracted branch builds without depending on files outside the subtree.
- The push target remote points at the intended GitHub repository.
