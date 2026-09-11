export class ApiError extends Error {
  readonly status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

interface ProblemDetails {
  title?: string | null;
  detail?: string | null;
  errors?: Record<string, string[]>;
}

export function errorMessage(error: unknown, fallback: string): string {
  // Not every action returns a ProblemDetails body — CatalogController.Search's 400, for
  // example, is a bare string (`BadRequest("The search provider is currently unavailable.")`).
  if (typeof error === 'string' && error.length > 0) {
    return error;
  }
  const problem = error as ProblemDetails | undefined;
  if (problem?.errors) {
    const messages = Object.values(problem.errors).flat();
    if (messages.length > 0) {
      return messages.join(' ');
    }
  }
  return problem?.detail ?? problem?.title ?? fallback;
}

// Several backend actions can fail with a status their OpenAPI doc doesn't declare (no
// [ProducesResponseType] attribute for the error case), which makes openapi-fetch's generated
// "error" type `never` for that call — checking Response.ok directly, rather than trusting that
// generated type, is what actually reflects what the server can really do.
export function unwrap<T>(
  result: { data?: T; error?: unknown; response: Response },
  fallback: string,
): T {
  if (!result.response.ok) {
    throw new ApiError(
      errorMessage(result.error, fallback),
      result.response.status,
    );
  }
  return result.data as T;
}

// Every message an ApiError carries was deliberately curated by `errorMessage()` above (or an
// explicit fallback) — safe to show as-is. Anything else — a network failure, an unexpected
// exception, a bug elsewhere — is not: its `.message` could be raw technical detail (a stray
// SyntaxError from a malformed response body, once literally happened here) never meant for a
// user to see. Pages should always go through this rather than reading `err.message` directly.
export function userFacingMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? error.message : fallback;
}
