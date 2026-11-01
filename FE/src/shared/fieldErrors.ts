import type { ValidateRes } from '../api/types'

/**
 * Maps the envelope's `validates` array (`Request.Validation.Failed` —
 * `ApiBehaviorConfiguration.InvalidModelStateResponseFactory`) to a
 * lower-cased-key lookup so a form can show one message under its own field
 * regardless of the exact casing ASP.NET's model binder used for the key
 * (the request's C# property name, e.g. `Title`, `DueDate`).
 */
export function fieldErrorMap(validates: ValidateRes[] | null | undefined): Record<string, string> {
  const map: Record<string, string> = {}

  for (const entry of validates ?? []) {
    if (entry.errors.length > 0) {
      map[entry.key.toLowerCase()] = entry.errors[0]
    }
  }

  return map
}
