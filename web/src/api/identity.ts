import { setTokens } from '../auth/tokenStorage.ts';
import { apiClient } from './client.ts';
import { unwrap } from './errors.ts';
import type { components } from './generated/openapi-types.ts';

export type AccountInfo = components['schemas']['InfoResponse'];

export async function register(email: string, password: string): Promise<void> {
  const result = await apiClient.POST('/api/auth/register', {
    body: { email, password },
  });
  unwrap(result, 'Registration failed.');
}

export async function login(email: string, password: string): Promise<void> {
  const result = await apiClient.POST('/api/auth/login', {
    body: { email, password },
  });
  const tokens = unwrap(result, 'Login failed.');
  setTokens(tokens.accessToken, tokens.refreshToken);
}

export async function forgotPassword(email: string): Promise<void> {
  const result = await apiClient.POST('/api/auth/forgotPassword', {
    body: { email },
  });
  unwrap(result, 'Could not send a password reset link.');
}

export async function resetPassword(
  email: string,
  resetCode: string,
  newPassword: string,
): Promise<void> {
  const result = await apiClient.POST('/api/auth/resetPassword', {
    body: { email, resetCode, newPassword },
  });
  unwrap(result, 'Could not reset the password.');
}

export async function confirmEmail(
  userId: string,
  code: string,
  changedEmail?: string,
): Promise<void> {
  // This endpoint's success response is a plain-text message ("Thank you for confirming your
  // email."), not JSON — openapi-fetch defaults to parsing every response as JSON, which throws
  // a raw SyntaxError on this one. parseAs: 'text' avoids that; we only care about the status.
  const result = await apiClient.GET('/api/auth/confirmEmail', {
    params: { query: { userId, code, changedEmail } },
    parseAs: 'text',
  });
  unwrap(result, 'Could not confirm the email address.');
}

export async function getAccountInfo(): Promise<AccountInfo> {
  const result = await apiClient.GET('/api/auth/manage/info');
  return unwrap(result, 'Could not load account information.');
}

export async function changePassword(
  oldPassword: string,
  newPassword: string,
): Promise<void> {
  const result = await apiClient.POST('/api/auth/manage/info', {
    body: { oldPassword, newPassword },
  });
  unwrap(result, 'Could not change the password.');
}
