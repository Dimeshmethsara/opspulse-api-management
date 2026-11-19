import { describe, expect, it } from 'vitest';
import { validateApiBaseUrl } from './api-config';

describe('API configuration', () => {
  it('preserves a later gateway base path and removes trailing slashes', () => {
    expect(validateApiBaseUrl('https://gateway.example/opspulse/v1/')).toBe('https://gateway.example/opspulse/v1');
  });
  it.each([undefined, 'javascript:alert(1)', 'https://user:secret@example.com', 'https://example.com?token=secret', 'https://example.com#fragment', '/api'])('rejects invalid or secret-bearing configuration: %s', value => {
    expect(() => validateApiBaseUrl(value)).toThrow();
  });
});
