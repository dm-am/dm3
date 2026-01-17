# Build Comparison: Old (b8331728) vs Current

**Date:** 2026-01-15
**Purpose:** Document differences between old stable build and current modifications

---

## Summary of Changes

### Status: ⚠️ Some Deviations Detected

The current build has several modifications from the original. Most are improvements, but some need review.

---

## 1. Package Version Changes

### Microsoft.Extensions.*

| Package | Old (b8331728) | Current | Status |
|---------|----------------|---------|--------|
| `Caching.Abstractions` | 8.0.0 | 9.0.0 | ⚠️ CHANGED |
| `Configuration` | 8.0.0 | 9.0.0 | ⚠️ CHANGED |
| `Configuration.EnvironmentVariables` | 8.0.0 | 9.0.0 | ⚠️ CHANGED |
| `Configuration.FileExtensions` | 8.0.1 | 9.0.0 | ⚠️ CHANGED |
| `Configuration.Json` | 8.0.0 | 9.0.0 | ⚠️ CHANGED |
| `DependencyInjection.Abstractions` | 8.0.1 | 9.0.0 | ⚠️ CHANGED |
| `Diagnostics.HealthChecks` | 8.0.8 | 9.0.0 | ⚠️ CHANGED |
| `FileProviders.Embedded` | 8.0.8 | 9.0.0 | ⚠️ CHANGED |
| `Hosting.Abstractions` | N/A | 9.0.0 | ➕ ADDED |
| `Options` | 8.0.2 | 9.0.0 | ⚠️ CHANGED |

**Reason:** OpenTelemetry packages required newer versions.

### OpenTelemetry.*

| Package | Old (b8331728) | Current | Status |
|---------|----------------|---------|--------|
| `Exporter.Prometheus.AspNetCore` | N/A | 1.10.0-beta.1 | ➕ ADDED |
| `Instrumentation.Runtime` | N/A | 1.9.0 | ➕ ADDED |

### Other

| Package | Old | Current | Status |
|---------|-----|---------|--------|
| `SixLabors.ImageSharp` | 3.1.5 | 3.1.8 | ⚠️ UPDATED |

---

## 2. New Files Added

### Core/Tracing

```
src/DM.Services.Core/Tracing/DmActivitySource.cs (NEW)
```

**Purpose:** Centralized activity source for distributed tracing.

```csharp
public static class DmActivitySource
{
    public const string Name = "DM.Services";
    public static readonly ActivitySource Source = new(Name, "1.0.0");
}
```

**Compliance:** ✅ Follows naming convention, has XML documentation.

### DataAccess/Outbox

```
src/DM.Services.DataAccess/BusinessObjects/Common/OutboxEvent.cs (NEW)
```

**Purpose:** Outbox pattern for reliable message delivery.

**Fix Applied:** Added `#nullable enable` directive.

---

## 3. Method Signature Changes

Several methods had CancellationToken parameters added but callers weren't updated.

### Fixed Files

| File | Issue | Fix |
|------|-------|-----|
| `MessageCreatingService.cs:60` | Passing CT to method that doesn't accept it | Removed CT |
| `MessageCreatingService.cs:68` | `Increment()` doesn't take 3 args | Removed CT |
| `MessageReadingService.cs:34` | Passing CT to method that doesn't accept it | Removed CT |
| `TopicCreatingService.cs:59,67` | Wrong signature | Removed CT |
| `TopicReadingService.cs:47,66,57,72,92` | Wrong signatures | Fixed |

---

## 4. Test File Updates

Tests needed updates for CancellationToken in Moq setups:

| File | Change |
|------|--------|
| `TopicReadingServiceShould.cs` | Added `It.IsAny<CancellationToken>()` |
| `TopicCreatingServiceShould.cs` | Added `It.IsAny<CancellationToken>()` |
| `TopicDeletingServiceShould.cs` | Added `It.IsAny<CancellationToken>()` |
| `LikeServiceForTopicsShould.cs` | Added `It.IsAny<CancellationToken>()` |
| `CommentaryReadingServiceShould.cs` | Added `It.IsAny<CancellationToken>()` |
| `CommentaryCreatingServiceShould.cs` | Added `It.IsAny<CancellationToken>()` |

---

## 5. Naming Convention Compliance

### Current Status

| Convention | Compliance | Notes |
|------------|------------|-------|
| Private fields: `_camelCase` | ✅ | Consistently followed |
| Interfaces: `IPascalCase` | ✅ | All interfaces prefixed |
| Classes: `PascalCase` | ✅ | Consistent |
| Methods: `PascalCase` | ✅ | Consistent |
| Test classes: `*Should.cs` | ✅ | All test classes follow |
| Modules: `*Module.cs` | ✅ | All Autofac modules follow |

---

## 6. File Renames (In Progress)

According to git status, these renames are staged:

| Old Name | New Name | Status |
|----------|----------|--------|
| `Fora/Forum.cs` | `Boards/Forum.cs` | RM → Renamed |
| `Fora/ForumModerator.cs` | `Boards/ForumModerator.cs` | RM |
| `Fora/ForumTopic.cs` | `Boards/ForumTopic.cs` | RM |
| `Fora/Poll.cs` | `Boards/Poll.cs` | RM |
| `reviews.ts` | `websiteReviews.ts` | RM |
| `ReviewList.vue` | `WebsiteReviewList.vue` | RM |

**Note:** These renames follow domain terminology changes.

---

## 7. Migrations Added

New migrations in current build:

```
20260114000000_RenameRecruitmentForum.cs
20260114100000_LimitLoginLength.cs
20260115000000_AddBoardDenormalizedFields.cs
20260115100000_UpdateBoardDescriptions.cs
20260115200000_AddOutboxEvent.cs
```

---

## 8. Recommendations

### Immediate Actions

1. **Review Package Upgrades**
   - The jump from Microsoft.Extensions 8.x to 9.x may have breaking changes
   - Test thoroughly before production deployment

2. **Complete Rename Refactoring**
   - Finish `Fora` → `Boards` rename consistently
   - Update all references in frontend

3. **Document New Features**
   - Outbox pattern implementation
   - OpenTelemetry metrics integration

### Standards Compliance

| Area | Status | Action |
|------|--------|--------|
| XML Documentation | ⚠️ | New classes need `///` comments |
| Nullable Context | ⚠️ | Some new files missing `#nullable enable` |
| Test Coverage | ✅ | All existing tests pass |

---

## 9. Build Status

```
Build: ✅ SUCCESS
Tests: ✅ 179 PASSED
Warnings: 0
Errors: 0
```

---

## 10. Rollback Notes

If rollback needed:

```bash
git checkout b8331728 -- Directory.Packages.props
git checkout b8331728 -- src/DM.Services.Core/DM.Services.Core.csproj
```

---

*Document generated: 2026-01-15*
