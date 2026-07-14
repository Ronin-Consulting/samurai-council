import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Sidebar } from './components/sidebar';
import { ThemeService } from './services/theme.service';
import { ViewModeService } from './services/view-mode.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Sidebar],
  templateUrl: './app.html',
})
export class App {
  theme = inject(ThemeService);
  viewMode = inject(ViewModeService);
}
