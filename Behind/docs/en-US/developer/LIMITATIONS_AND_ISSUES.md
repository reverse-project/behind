# Limitations and Known Issues

This document outlines known limitations, issues, and dependency vulnerabilities in the Behind project.

## Known Dependency Vulnerabilities

As of the latest update, all known transitive dependency vulnerabilities have been resolved via explicit version overrides in `Directory.Packages.props`. `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages across the solution.

### Current Status

**Framework Versions**:
- .NET: 10.0.10 (latest)
- ASP.NET Core: 10.0.8
- Entity Framework Core: 10.0.10 (latest)
- OpenTelemetry: 1.16.0 (latest)

### Unfixable Vulnerabilities

None currently known.

### Fixed Vulnerabilities

✅ **SQLitePCLRaw.lib.e_sqlite3**: Fixed by updating to 2.1.12

✅ **Entity Framework Core packages**: Updated to 10.0.10 (latest)

✅ **Microsoft.OpenApi (GHSA-v5pm-xwqc-g5wc)**: Transitive dependency pulled in via Scalar.AspNetCore 2.14.11. Fixed by pinning `Microsoft.OpenApi` to version 2.11.0 in `Directory.Packages.props` (patched in 2.7.5+ for the 2.X line).

✅ **MessagePack**: Transitive dependency pulled in via StreamJsonRpc (required by Aspire.Hosting). Fixed by pinning `MessagePack` to version 3.1.8 in `Directory.Packages.props`.

## Architectural Limitations

### 1. Single Database Context

**Limitation**: Only one DbContext (ApplicationDbContext) for all aggregates.

**Impact**:
- Cannot easily split into separate databases per service
- All aggregates share same transaction scope
- Harder to scale horizontally at database level

**Mitigation**:
- Use repository pattern abstraction for scaling considerations
- Plan for context split if scaling requirements change
- Current design sufficient for typical workloads

### 2. In-Memory Caching

**Limitation**: No built-in caching layer (Redis/Memcached).

**Impact**:
- Every query hits database
- No distributed cache support
- Performance on high-traffic scenarios

**Mitigation**:
- Add caching via Aspire (Valkey/Redis)
- Use EF Core query caching
- Implement result caching in repositories

**Recommendation**: Add caching layer before production at scale

### 3. No Event Sourcing

**Limitation**: Uses only traditional event logging via AuditLogEntry.

**Impact**:
- No audit trail of state changes over time
- Temporal queries difficult
- Compliance audit trails not complete

**Mitigation**:
- Detailed AuditLogEntry records with before/after values
- Archival strategy for old audit logs
- Manual audit log review for compliance

### 4. Limited Search Capabilities

**Limitation**: No full-text search implementation.

**Impact**:
- LIKE queries only for string matching
- No fuzzy search
- Poor performance on large text fields

**Mitigation**:
- Add Elasticsearch or similar via Aspire
- Implement search via separate service
- Index text fields for LIKE queries

## Functional Programming Limitations

### 1. Pattern Exhaustiveness

**Issue**: C# doesn't enforce exhaustive pattern matching like F#.

**Example**:
```csharp
// This compiles even though not all User types handled
var result = user switch
{
    HumanUser => "Human",
    SystemUser => "System"
    // Missing: GuestUser, BannedUser
};
```

**Mitigation**:
- Use `_ => throw new NotImplementedException()` as final case
- Add compiler warnings via .editorconfig
- Code review to catch missing cases

### 2. Option<T> vs null Coexistence

**Issue**: NRT (Nullable Reference Types) and Option<T> can conflict.

**Example**:
```csharp
public string? Name { get; init; }  // Could be null
public Option<string> Value { get; init; }  // Never null, always Some/None

// Confusing: which represents missing value?
```

**Mitigation**:
- Use Option<T> consistently for domain concepts
- Use null only for edge cases
- Document NRT usage clearly

### 3. Generic Constraints

**Issue**: Can't express all needed constraints in C#.

**Example**:
```csharp
// Would like: "T must be a record"
public interface IRepository<T> where T : record  // Not supported
{
}
```

**Mitigation**:
- Document requirements clearly
- Enforce via code review
- Use explicit interfaces for different aggregate types

## Performance Considerations

### Current Bottlenecks

1. **No Query Caching** - Every identical query hits database
2. **N+1 Problem Possible** - Must explicitly Include() related data
3. **No Connection Pooling Tuning** - Using defaults
4. **Single Database** - No read replicas

### Optimization Opportunities

- Add Redis caching layer
- Implement query result caching
- Add database read replicas
- Consider CQRS for heavy read workloads
- Batch operations where possible

## Security Limitations

### 1. JWT Token Revocation

**Limitation**: JWT tokens are valid until expiration; no revocation list.

**Impact**:
- Can't immediately revoke tokens (e.g., after logout)
- Security risk for compromised tokens

**Mitigation**:
- Short token expiration (15-30 minutes)
- Refresh token rotation
- Implement revocation list for critical scenarios
- Monitor session invalidation

### 2. Password Complexity

**Limitation**: Basic regex validation only.

**Impact**:
- Doesn't check against common passwords
- No dictionary attack prevention

**Mitigation**:
- Add HaveIBeenPwned API integration
- Use password strength estimation (zxcvbn algorithm)
- Implement rate limiting on authentication

### 3. No Rate Limiting

**Limitation**: No built-in rate limiting on endpoints.

**Impact**:
- Brute force attacks possible
- API abuse not prevented
- DOS vulnerability

**Mitigation**:
- Add rate limiting middleware (AspNetCore.RateLimiting)
- Implement per-user rate limits
- Use API gateway in production

### 4. CORS Configuration

**Limitation**: CORS setup must be explicit per deployment.

**Impact**:
- Misconfiguration risk
- Development vs production differences

**Mitigation**:
- Use environment-specific CORS policies
- Document CORS setup clearly
- Review CORS configuration in security audits

## Testing Limitations

### 1. No E2E Tests

**Limitation**: Only unit tests provided; no end-to-end tests.

**Impact**:
- Integration issues not caught
- Full flow validation missing
- Deployment surprises

**Recommendation**:
- Add integration tests using WebApplicationFactory
- Add E2E tests with test containers
- Include in CI/CD pipeline

### 2. Limited Test Database Support

**Limitation**: Tests use SQLite in memory; production uses SQLite on disk.

**Impact**:
- Test/production mismatch
- SQLite-specific issues not caught until production

**Recommendation**:
- Add tests against PostgreSQL
- Use TestContainers for consistent testing
- Test against actual target database

### 3. Mock Repository Issues

**Limitation**: Mocking DbSet and IQueryable is complex.

**Impact**:
- Repository tests often brittle
- Async query operators hard to mock

**Recommendation**:
- Use InMemory EF Core provider for tests
- Leverage TestContainers
- Minimize complex LINQ in repositories

## Deployment Limitations

### 1. No Container Security Scanning

**Limitation**: No automated vulnerability scanning in build pipeline.

**Impact**:
- Vulnerable base images used
- Runtime vulnerabilities not detected

**Recommendation**:
- Add Trivy or Grype scanning
- Implement policy enforcement
- Regular base image updates

### 2. Single Instance Deployment

**Limitation**: Architecture supports single instance only without extra work.

**Impact**:
- No high availability
- Single point of failure
- Scaling requires architectural changes

**Recommendation**:
- Use load balancer for multiple instances
- Implement distributed session management
- Plan for database replication

### 3. No Blue-Green Deployment

**Limitation**: No built-in blue-green deployment strategy.

**Impact**:
- Downtime during updates
- Rollback difficult

**Recommendation**:
- Implement via Kubernetes
- Use infrastructure as code
- Plan zero-downtime deployment strategy

## Data Limitations

### 1. No Data Encryption at Rest

**Limitation**: Database files not encrypted.

**Impact**:
- Disk theft exposes data
- Compliance violations

**Recommendation**:
- Enable SQLite encryption (SEE)
- Use PostgreSQL with encryption extensions
- Implement at storage level (EBS encryption, etc.)

### 2. No Backup Strategy

**Limitation**: No built-in backup/restore mechanism.

**Impact**:
- Data loss on disk failure
- No recovery point

**Recommendation**:
- Implement daily backups
- Test restore procedures regularly
- Use managed database services

### 3. Limited Audit Trail

**Limitation**: AuditLogEntry doesn't capture all changes.

**Impact**:
- Compliance gaps
- Incomplete audit trails

**Recommendation**:
- Capture all entity changes
- Implement change data capture (CDC)
- Archive audit logs separately

## Platform Limitations

### 1. .NET Version

**Limitation**: Tied to .NET ecosystem.

**Impact**:
- Windows/Linux only (no macOS server)
- Limited to C# ecosystem libraries

**Trade-off**: Accepted for project scope

### 2. SQLite Limitations (Development)

**Limitations**:
- No concurrent writes
- Limited query optimization
- No stored procedures
- No advanced features

**Mitigation**:
- Use SQLite for development only
- PostgreSQL recommended for production
- Acknowledge SQLite trade-offs

## Operational Limitations

### 1. No Monitoring Dashboard

**Limitation**: No built-in monitoring UI.

**Impact**:
- Must use external tools (Grafana, Datadog)
- No health check UI

**Recommendation**:
- Implement health checks endpoint
- Add metrics dashboards
- Use APM tools

### 2. No Admin Interface

**Limitation**: No admin UI for operational tasks.

**Impact**:
- Admin operations via API only
- No web-based management interface

**Recommendation**:
- Build admin dashboard
- Implement operational endpoints
- Document API for admin use

### 3. Limited Logging Configuration

**Limitation**: OpenTelemetry setup complex; limited UI logging.

**Impact**:
- Operational visibility difficult
- Debugging production issues harder

**Recommendation**:
- Implement structured logging queries
- Add logging UI/dashboard
- Document common queries

## Compatibility Notes

### Supported Platforms
- ✅ Windows Server 2016+
- ✅ Linux (Ubuntu 20.04+, CentOS 7+, Debian 10+)
- ✅ macOS (for development only)
- ❌ Docker Swarm (use Kubernetes)
- ⚠️ Azure App Service (potential issues with certain extensions)

### Supported Databases
- ✅ SQLite 3.4+ (development)
- ✅ PostgreSQL 12+ (production recommended)
- ❌ SQL Server (not currently supported)
- ❌ MySQL (not currently supported)

### Supported Browsers (for API docs)
- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Safari 14+
- ✅ Edge 90+

## Reporting Issues

If you discover security vulnerabilities:
1. **DO NOT** open public GitHub issues
2. Email zoltan@abel.co.hu with details
3. Allow 48 hours for acknowledgment
4. Coordinate disclosure timeline

For non-security issues:
1. Search existing issues first
2. Open GitHub issue with reproduction steps
3. Include environment details
4. Expected vs actual behavior

## Summary

| Category | Status | Impact |
|----------|--------|--------|
| Dependency Security | ⚠️ Unfixable vulnerabilities | Moderate (dev/staging) |
| Performance | ⚠️ No caching | Moderate |
| Scalability | ⚠️ Single instance | Medium workloads only |
| Security | ⚠️ No rate limiting | High-risk endpoints |
| Operations | ⚠️ Limited monitoring | Manual oversight required |
| Testing | ⚠️ Unit only | Integration gaps |

---

**Remember**: This codebase is production-ready for small to medium workloads. Plan architectural improvements for enterprise scale deployments.

For questions about limitations, contact the development team or open a discussion issue.
