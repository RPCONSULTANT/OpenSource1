# Branch protection setup (`QA`, `deploy` and `main`)

Use GitHub **Settings > Branches** to configure branch protection rules.

## Rule for `QA` (or `qa`)

Enable:

- Require a pull request before merging
- Require status checks to pass before merging
- Require branches to be up to date before merging

Set required checks:

- `PR to QA - Validate / Restore, Build and Test`

## Rule for `deploy`

Enable:

- Require a pull request before merging
- Require status checks to pass before merging
- Require branches to be up to date before merging

Set required checks:

- `PR to Deploy - Enforce QA and Validate / Enforce source branch QA`
- `PR to Deploy - Enforce QA and Validate / Restore, Build and Test`

## Rule for `main`

Enable:

- Require a pull request before merging
- Require status checks to pass before merging
- Require branches to be up to date before merging
- Restrict who can push to matching branches (optional but recommended)

Set required checks:

- `PR to Main - Enforce Deploy and Validate / Enforce source branch deploy`
- `PR to Main - Enforce Deploy and Validate / Restore, Build and Test`

Also enable:

- Allow auto-merge (repository setting in **Settings > General > Pull Requests**)

With these checks, any PR into `main` from a source branch other than `deploy` is blocked, and the workflow `Promote Deploy to Main - Auto PR` can auto-merge once checks pass.

## Validation scenarios

Expected behavior after enabling the rules:

1. `feature/* -> QA`: allowed only after `PR to QA - Validate` passes.
2. `feature/* -> deploy`: rejected by `Enforce source branch QA`.
3. `QA -> deploy`: allowed only after both deploy checks pass.
4. `deploy -> main`: PR is created automatically and can merge automatically after main checks pass.
5. `feature/* -> main`: rejected by `Enforce source branch deploy`.
