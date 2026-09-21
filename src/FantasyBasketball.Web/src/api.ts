export type Envelope<T> = {
  success: boolean; data: T; error: { code: string; message: string; fields: Record<string, string[]> } | null;
  meta: { total: number; page: number; limit: number } | null;
};
export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string) { super(message); }
}

// Cookies stay HttpOnly. Fetch a fresh token for each mutation, including after
// login/logout: a token issued to the anonymous identity cannot be reused.
export async function api<T>(path: string, options: RequestInit = {}): Promise<Envelope<T>> {
  const method = options.method ?? 'GET';
  const headers = new Headers(options.headers);
  if (method !== 'GET') {
    const csrf = await api<{ token: string }>('/api/account/antiforgery');
    headers.set('X-CSRF-TOKEN', csrf.data.token);
    headers.set('Content-Type', 'application/json');
  }
  const response = await fetch(path, { ...options, method, headers, credentials: 'same-origin', cache: 'no-store' });
  const body: Envelope<T> = await response.json().catch(() => {
    throw new ApiError(response.status, 'invalid_response', 'The server returned an unreadable response. Please retry.');
  });
  if (!response.ok || !body.success) {
    if (response.status === 401 && !path.startsWith('/api/account/')) window.dispatchEvent(new Event('session-expired'));
    const fields = Object.values(body.error?.fields ?? {}).flat().join(' ');
    throw new ApiError(response.status, body.error?.code ?? 'request_failed', fields || body.error?.message || 'The request failed.');
  }
  return body;
}
export const post = <T>(path: string, data: unknown) => api<T>(path, { method: 'POST', body: JSON.stringify(data) });
export const message = (error: unknown) => error instanceof Error ? error.message : 'Something went wrong. Please retry.';
