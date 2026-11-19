import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { AppComponent } from './app/app.component';
import { API_BASE_URL, loadApiBaseUrl } from './app/core/api-config';

async function main(): Promise<void> {
  const apiBaseUrl = await loadApiBaseUrl();
  await bootstrapApplication(AppComponent, {
    providers: [provideHttpClient(), { provide: API_BASE_URL, useValue: apiBaseUrl }],
  });
}

main().catch(() => {
  // Text only: never insert the configuration value or server errors as HTML.
  const message = document.createElement('p');
  message.className = 'startup-error';
  message.textContent = 'OpsPulse could not start. Check config.json and reload the page.';
  document.body.appendChild(message);
});
