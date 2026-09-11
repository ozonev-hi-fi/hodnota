import { describe, expect, it } from 'vitest';
import { ApiError, errorMessage, unwrap, userFacingMessage } from './errors.ts';

describe('errorMessage', () => {
  it('extracts messages from a ProblemDetails errors map', () => {
    expect(
      errorMessage(
        { errors: { Field: ['A message.', 'Another.'] } },
        'fallback',
      ),
    ).toBe('A message. Another.');
  });

  it('falls back to detail, then title, then the fallback text', () => {
    expect(errorMessage({ detail: 'Detail text.' }, 'fallback')).toBe(
      'Detail text.',
    );
    expect(errorMessage({ title: 'Title text.' }, 'fallback')).toBe(
      'Title text.',
    );
    expect(errorMessage({}, 'fallback')).toBe('fallback');
  });

  it('returns a plain string error body as-is', () => {
    expect(
      errorMessage('The search provider is currently unavailable.', 'fallback'),
    ).toBe('The search provider is currently unavailable.');
  });
});

describe('unwrap', () => {
  it('returns data when the response is ok', () => {
    const data = unwrap(
      { data: { ok: true }, response: new Response(null, { status: 200 }) },
      'fallback',
    );

    expect(data).toEqual({ ok: true });
  });

  it('throws an ApiError carrying the response status when not ok', () => {
    expect(() =>
      unwrap(
        { error: 'Bad.', response: new Response(null, { status: 400 }) },
        'fallback',
      ),
    ).toThrow(expect.objectContaining({ message: 'Bad.', status: 400 }));
  });
});

describe('userFacingMessage', () => {
  it('shows the message of a curated ApiError', () => {
    expect(
      userFacingMessage(new ApiError('Invalid credentials.', 401), 'fallback'),
    ).toBe('Invalid credentials.');
  });

  // This is the actual bug this function exists to prevent: a raw SyntaxError from a JSON-parse
  // failure once leaked its own message ("Unexpected token 'T', ... is not valid JSON") onto a
  // page. Any non-ApiError exception must fall back to a safe, generic message instead.
  it('never shows the message of an uncurated error', () => {
    expect(
      userFacingMessage(
        new SyntaxError("Unexpected token 'T' is not valid JSON"),
        'fallback',
      ),
    ).toBe('fallback');
    expect(
      userFacingMessage(new TypeError('Failed to fetch'), 'fallback'),
    ).toBe('fallback');
    expect(userFacingMessage('a raw string', 'fallback')).toBe('fallback');
    expect(userFacingMessage(undefined, 'fallback')).toBe('fallback');
  });
});
