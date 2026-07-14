import { Component, input } from '@angular/core';
import { TableData } from '../../models';
import { StudioCard } from './studio-card';

/** Pre-made table card (zebra rows, sticky header, row-count footer). Used for ChartType 'Table'. */
@Component({
  selector: 'app-studio-table',
  imports: [StudioCard],
  template: `
    <app-studio-card title="Results" badge="Table" [footer]="table().rows.length + ' rows'">
      <div class="max-h-96 overflow-auto rounded-lg">
        <table class="w-full text-sm">
          <thead class="sticky top-0 bg-neutral-100 dark:bg-neutral-800">
            <tr>
              @for (col of table().columns; track col) {
                <th class="px-3 py-2 text-left font-semibold whitespace-nowrap">{{ col }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of table().rows; track $index; let i = $index) {
              <tr [class]="i % 2 ? 'bg-neutral-50 dark:bg-neutral-800/30' : ''">
                @for (cell of row; track $index) {
                  <td class="px-3 py-1.5 tabular-nums whitespace-nowrap">{{ cell }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    </app-studio-card>
  `,
})
export class StudioTable {
  table = input.required<TableData>();
}
