import { cleanup } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';

// Without this, the DOM from one test's render() stays mounted for the next test in the same
// file (Testing Library only auto-cleans up when a global `afterEach` is detected, and this
// project's Vitest config doesn't enable test.globals) — harmless for a single render() per
// file, but causes "multiple elements found" once a file renders more than once.
afterEach(cleanup);
