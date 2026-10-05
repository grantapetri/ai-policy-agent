# Secure C# Application Development Policy

## Purpose

This policy defines basic security requirements that all application code must follow

## Rules

### SEC-001 - No Hardcoded Secrets

API keys, passwords, access tokens, connection strings, and other secrets must never be hardcoded directly into source code.

Secrets must be loaded from environment variables, a secret configuration system, or a dedicated secret-management service.

**Security:** Critical

---

### SEC-002 - Password Protection

User passwords must never be stored in plaintext.

Passwords must be securely hashed using an approved password-hashing algorithm

**Severity:** Critical

---

### SEC-003 - Input Validation

Al user-controlled input must be validated before being used by the application.

The application must not blindy trust values received from users, HTTP requests, external APIs, or other untrusted sources.

**Severity:** High

---

### SEC-004 - SQL Injection Prevention

User-controlled values must never be directly concatenated into SQL queries.

Parameterized queries or an equivalent safe database abstraction must be used.
