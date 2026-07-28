import { Component, computed, inject, input, signal } from '@angular/core';
import { MarkdownComponent } from 'ngx-markdown';
import { AssistantMessage } from '../models';
import { VisualDisplay } from './visual-display';
import { StudioDisplay } from './studio/studio-display';
import { ApiService } from '../services/api.service';
import { ViewModeService } from '../services/view-mode.service';
import { deAnonymize, getProviderColor, getProviderInitial, getShortModelName } from '../util';

type Tab = 'final' | 'responses' | 'rankings';

@Component({
  selector: 'app-assistant-message',
  imports: [MarkdownComponent, VisualDisplay, StudioDisplay],
  template: `
    @if (message(); as m) {
      @if (m.loading; as l) {
        <!-- Progressive loading indicator -->
        <div class="rounded-xl border border-neutral-200 dark:border-neutral-700 p-5">
          <div class="mb-4 text-sm font-medium text-neutral-700 dark:text-neutral-200">{{ loadingLabel() }}</div>
          <div class="flex items-center">
            @for (s of steps(); track s.n; let last = $last) {
              <div class="flex items-center gap-2">
                <span [class]="dotClass(s.state)">
                  @if (s.state === 'done') { ✓ } @else { {{ s.n }} }
                </span>
                <span [class]="labelClass(s.state)">{{ s.label }}</span>
              </div>
              @if (!last) {
                <span class="mx-3 h-px w-8 flex-none"
                  [class]="s.state === 'done' ? 'bg-green-600/50' : 'bg-neutral-200 dark:bg-neutral-700'"></span>
              }
            }
          </div>
        </div>
      } @else {
        <div class="rounded-xl border border-neutral-200 dark:border-neutral-700 overflow-hidden">
          <!-- Tabs -->
          <div class="flex border-b border-neutral-200 dark:border-neutral-700 text-sm">
            <button (click)="tab.set('final')" [class]="tabClass('final')" [disabled]="!m.stage3">Final Answer</button>
            <button (click)="tab.set('responses')" [class]="tabClass('responses')">Responses ({{ m.stage1?.length ?? 0 }})</button>
            <button (click)="tab.set('rankings')" [class]="tabClass('rankings')">Rankings ({{ m.stage2?.length ?? 0 }})</button>
          </div>

          <div class="p-4">
            <!-- FINAL -->
            @if (tab() === 'final' && m.stage3; as s3) {
              <div class="flex items-center justify-between mb-3">
                <span class="inline-flex items-center gap-1.5 rounded-full bg-red-600/10 px-2.5 py-1 text-xs font-medium text-red-600">
                  <span class="inline-block h-1.5 w-1.5 rounded-full bg-red-600"></span>
                  Chairman · {{ short(s3.model) }}
                </span>
                @if (conversationId()) {
                  <span class="flex gap-2">
                    <a [href]="pdfUrl()" class="rounded-md border border-neutral-200 px-2.5 py-1 text-xs text-neutral-600 transition-colors hover:bg-neutral-100 dark:border-neutral-700 dark:text-neutral-300 dark:hover:bg-neutral-800">PDF</a>
                    <a [href]="xlsxUrl()" class="rounded-md border border-neutral-200 px-2.5 py-1 text-xs text-neutral-600 transition-colors hover:bg-neutral-100 dark:border-neutral-700 dark:text-neutral-300 dark:hover:bg-neutral-800">Excel</a>
                    <a [href]="docxUrl()" class="rounded-md border border-neutral-200 px-2.5 py-1 text-xs text-neutral-600 transition-colors hover:bg-neutral-100 dark:border-neutral-700 dark:text-neutral-300 dark:hover:bg-neutral-800">Word</a>
                  </span>
                }
              </div>
              <markdown class="prose prose-sm answer answer-final" [data]="s3.response"></markdown>
              @if (viewMode.mode() === 'studio') {
                @if (s3.studio_chart) { <app-studio-display [recommendation]="s3.studio_chart" /> }
              } @else {
                @if (s3.chart) { <app-visual-display [recommendation]="s3.chart" /> }
              }
            }

            <!-- RESPONSES -->
            @if (tab() === 'responses') {
              @for (r of m.stage1 ?? []; track r.model) {
                <div class="mb-4">
                  <div class="flex items-center gap-2 mb-1.5">
                    <span class="inline-flex h-6 w-6 items-center justify-center rounded-full text-white text-xs {{ color(r.model) }}">{{ initial(r.model) }}</span>
                    <span class="font-medium text-sm">{{ short(r.model) }}</span>
                    @if (r.tool_usages.length) { <span class="rounded-full border border-neutral-200 px-2 py-0.5 text-[11px] text-neutral-500 dark:border-neutral-700">🔧 tools</span> }
                  </div>
                  <markdown class="prose prose-sm answer" [data]="r.response"></markdown>
                </div>
              }
            }

            <!-- RANKINGS -->
            @if (tab() === 'rankings') {
              @if (m.metadata?.aggregate_rankings?.length) {
                <table class="w-full text-sm mb-5 tabular-nums">
                  <thead>
                    <tr class="text-left text-[11px] font-semibold uppercase tracking-wide text-neutral-400">
                      <th class="pb-2 font-semibold">#</th><th class="pb-2 font-semibold">Model</th><th class="pb-2 text-right font-semibold">Avg Rank</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (a of m.metadata!.aggregate_rankings; track a.model; let i = $index) {
                      <tr class="border-t border-neutral-200 dark:border-neutral-800">
                        <td class="py-1.5 text-neutral-400">{{ i + 1 }}</td>
                        <td class="py-1.5">{{ short(a.model) }}</td>
                        <td class="py-1.5 text-right">{{ a.average_rank.toFixed(2) }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
              @for (rk of m.stage2 ?? []; track rk.model) {
                <div class="mb-3">
                  <div class="font-medium text-sm mb-1">{{ short(rk.model) }}</div>
                  <markdown class="prose prose-sm answer" [data]="deanon(rk.ranking, m.metadata?.label_to_model)"></markdown>
                </div>
              }
            }
          </div>
        </div>
      }
    }
  `,
})
export class AssistantMessagePanel {
  private api = inject(ApiService);
  viewMode = inject(ViewModeService);
  message = input.required<AssistantMessage>();
  query = input<string>('');
  conversationId = input<string>('');

  tab = signal<Tab>('final');

  short = getShortModelName;
  color = getProviderColor;
  initial = getProviderInitial;
  deanon = deAnonymize;

  pdfUrl = computed(() => this.api.exportUrl(this.conversationId(), 'pdf'));
  xlsxUrl = computed(() => this.api.exportUrl(this.conversationId(), 'xlsx'));
  docxUrl = computed(() => this.api.exportUrl(this.conversationId(), 'docx'));

  loadingLabel = computed(() => {
    const l = this.message().loading;
    if (l?.stage3) return 'Synthesizing the final answer…';
    if (l?.stage2) return 'Peer-reviewing responses…';
    return 'Collecting responses from the council…';
  });

  private currentStep = computed(() => {
    const l = this.message().loading;
    if (l?.stage3) return 3;
    if (l?.stage2) return 2;
    return 1;
  });

  steps = computed(() => {
    const active = this.currentStep();
    return [
      { n: 1, label: 'Responses' },
      { n: 2, label: 'Review' },
      { n: 3, label: 'Synthesis' },
    ].map((d) => ({
      ...d,
      state: d.n < active ? 'done' : d.n === active ? 'active' : 'todo',
    }));
  });

  dotClass(state: string): string {
    const base = 'flex h-6 w-6 flex-none items-center justify-center rounded-full text-xs font-semibold ';
    if (state === 'done') return base + 'bg-green-600 text-white';
    if (state === 'active') return base + 'bg-red-600 text-white animate-pulse';
    return base + 'border border-neutral-300 text-neutral-400 dark:border-neutral-600';
  }

  labelClass(state: string): string {
    if (state === 'active') return 'text-sm font-medium text-neutral-900 dark:text-neutral-100';
    return 'text-sm text-neutral-400';
  }

  tabClass(t: Tab): string {
    const on = this.tab() === t;
    return 'px-4 py-2.5 -mb-px border-b-2 transition-colors ' + (on
      ? 'border-red-600 text-neutral-900 dark:text-neutral-100 font-medium'
      : 'border-transparent text-neutral-500 hover:text-neutral-800 dark:hover:text-neutral-200');
  }
}
