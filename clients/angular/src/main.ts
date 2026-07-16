import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// PrismJS: loading the core self-registers `window.Prism`, which ngx-markdown
// detects and uses to syntax-highlight fenced code blocks. Language grammars
// must be imported after the core. Token colors are themed in styles.css.
import 'prismjs';
import 'prismjs/components/prism-sql';
import 'prismjs/components/prism-json';
import 'prismjs/components/prism-bash';
import 'prismjs/components/prism-python';
import 'prismjs/components/prism-typescript';
import 'prismjs/components/prism-csharp';

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
