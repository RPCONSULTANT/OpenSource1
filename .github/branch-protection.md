# Branch protection setup (`qa` and `main`)

Use GitHub **Settings > Branches** to configure branch protection rules.

## Rule for `qa`

Enable:

- Require a pull request before merging
- Require status checks to pass before merging
- Require branches to be up to date before merging

Set required checks:

- `PR to QA - Validate / Restore, Build and Test`

## Rule for `main`

Enable:

- Require a pull request before merging
- Require status checks to pass before merging
- Require branches to be up to date before merging
- Restrict who can push to matching branches (optional but recommended)

Set required checks:

- `PR to Main - Enforce QA and Validate / Enforce source branch qa`
- `PR to Main - Enforce QA and Validate / Restore, Build and Test`

With these checks, any PR into `main` from a source branch other than `qa` is blocked.

## Validation scenarios

Expected behavior after enabling the rules:

1. `feature/* -> qa`: allowed only after `PR to QA - Validate` passes.
2. `feature/* -> main`: rejected by `Enforce source branch qa`.
3. `qa -> main`: allowed only after both main checks pass.
