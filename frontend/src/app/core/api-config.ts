import { InjectionToken } from '@angular/core';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

export function validateApiBaseUrl(value: unknown): string {
  if (typeof value !== 'string') throw new Error('An API URL is required.');
  const url = new URL(value);
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash)
    throw new Error('Use an HTTP(S) API URL without credentials, query or fragment.');
  return url.toString().replace(/\/+$/, '');
}

export async function loadApiBaseUrl(): Promise<string> {
  const response = await fetch('config.json', { cache: 'no-store' });
  if (!response.ok) throw new Error('Configuration unavailable.');
  const config: { apiBaseUrl?: unknown } = await response.json();
  return validateApiBaseUrl(config.apiBaseUrl);
}
