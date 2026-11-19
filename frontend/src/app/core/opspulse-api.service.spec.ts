import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { API_BASE_URL } from './api-config';
import { OpsPulseApi } from './opspulse-api.service';

describe('OpsPulseApi', () => {
  let http: HttpTestingController;
  let api: OpsPulseApi;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: 'https://gateway.example/opspulse/v1' }] });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(OpsPulseApi);
  });
  afterEach(() => http.verify());

  it('uses the configured base path and sends no operator credentials', () => {
    api.getStatus().subscribe();
    const request = http.expectOne('https://gateway.example/opspulse/v1/api/status');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.headers.has('X-OpsPulse-Key')).toBe(false);
    request.flush({ services: [] });
  });

  it('provides bounded read access to incident history', () => {
    api.getIncidents().subscribe();
    const request = http.expectOne('https://gateway.example/opspulse/v1/api/incidents?limit=100');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });
});
