# AI Policy-as-Code Enforcement Agent

An AI-powered CI/CD tool that automatically checks code changes against defined security policies.

## How It Works

```text
Git Push → GitHub Actions → Git Diff → AI Policy Check → PASS / FAIL
```

The agent:

* Reads security policies from `policy.md`
* Uses embeddings to find relevant policies
* Uses GPT to evaluate code changes
* Fails the CI workflow when a violation is detected

## Example

**Code change:**

```csharp
string apiKey = "my-super-secret-key";
```

**AI result:**

```text
Decision: FAIL
Policy: SEC-001
Reason: Hardcoded API key detected.
```

A compliant change, such as loading the key from an environment variable, passes the check.

## Technologies

* Python
* OpenAI GPT & Embeddings
* Git / GitHub
* GitHub Actions
* NumPy

## Project Goal

Turn written security policies into an **automated CI/CD enforcement mechanism** that checks code as it is pushed.
