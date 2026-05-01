Here’s a much clearer, structured, and agent-ready specification of your requirements:

---

# 📄 Refactor Spec: Social Providers → `Result<T, TError>` Pattern

## 🎯 Objective

Refactor all social providers to align with the new **railway-oriented programming pattern** using:

* `Result<T, TError>`
* Located at: `src\Aero.Core\Railway\Result.cs`

This refactor must ensure:

* **Consistency with updated abstractions**
* **Successful compilation of the solution**
* **Minimal, non-invasive changes**

---

## 📦 Scope

### ✅ Files Already Updated (DO NOT MODIFY)

* `src\Aero.Social\Abstractions\SocialProviderBase.cs`
* `src\Aero.Social\Abstractions\ISocialProvider.cs`

These now define the **new contract** using `Result<T, TError>`.

---

### 🔧 Files TO MODIFY

* All providers located in:

  ```
  src\Aero.Social\Providers\
  ```

* Any affected test files (likely under test projects referencing these providers)

---

## 🧩 Requirements

### 1. Update Method Signatures

* Ensure all provider methods match the updated interface (`ISocialProvider`)
* Replace existing return types with:

  ```csharp
  Result<TValue, TError>
  ```

---

### 2. Adapt Return Logic (Minimal Changes Only)

#### ✅ Allowed Changes

* Wrap successful results:

  ```csharp
  return new Result<TError, TValue>.Ok(value);
  ```

  OR implicit conversion if supported:

  ```csharp
  return value;
  ```

* Convert failures to:

  ```csharp
  return new Result<TError, TValue>.Failure(error);
  ```

  OR implicit conversion:

  ```csharp
  return error;
  ```

* Add `try/catch` ONLY if necessary to convert thrown exceptions into `Failure`

---

#### 🚫 Disallowed Changes

* ❌ Do NOT alter core business logic
* ❌ Do NOT refactor control flow beyond what is required
* ❌ Do NOT introduce new abstractions or patterns
* ❌ Do NOT rename methods, classes, or files
* ❌ Do NOT change dependencies or architecture

---

### 3. Exception Handling Rules

* If a method previously:

  * Returned `null` → convert to `Failure`
  * Threw exceptions → optionally wrap in `Failure` (only if needed to satisfy interface)

* Avoid over-catching:

  * Only catch exceptions where required for compatibility

---

### 4. Update Tests

#### Required Changes:

* Update assertions to handle `Result<T, TError>` instead of raw values

#### Example:

**Before:**

```csharp
var result = await provider.DoSomething();
Assert.NotNull(result);
```

**After:**

```csharp
var result = await provider.DoSomething();
Assert.True(result.IsSuccess);
Assert.NotNull(((Result<Error, Value>.Ok)result).Value);
```

OR (preferred pattern if helpers exist):

```csharp
result.IsSuccess.ShouldBeTrue();
result.Value.ShouldNotBeNull();
```

---

### 5. Compilation Requirement

* The following solution MUST compile successfully:

  ```
  src/Aero.Cms.slnx
  ```

---

## ⚙️ Implementation Strategy (Agent Guidance)

1. Scan all provider classes under:

   ```
   src\Aero.Social\Providers\
   ```

2. For each method:

   * Match signature to `ISocialProvider`
   * Replace return type
   * Wrap outputs in `Result<T, TError>`

3. Fix compile errors incrementally:

   * Start from providers → then tests

4. Run/build solution:

   ```
   dotnet build src/Aero.Cms.slnx
   ```

5. Update tests last

---

## ✅ Acceptance Criteria

* [ ] All providers compile with new interface
* [ ] No business logic changes introduced
* [ ] All methods return `Result<T, TError>`
* [ ] Tests updated and passing (or compiling if execution not required)
* [ ] Solution builds successfully

---

## ⚠️ Key Principle

> **This is a mechanical refactor, NOT a redesign.**
> Only change what is required to support `Result<T, TError>`.

---

If you want, I can also generate a **codemod-style transformation plan** (regex + Roslyn approach) to automate most of this across your solution.
