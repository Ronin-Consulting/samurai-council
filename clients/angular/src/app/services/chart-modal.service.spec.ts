import { ChartModalService } from './chart-modal.service';

describe('ChartModalService', () => {
  afterEach(() => {
    document.body.classList.remove('overflow-hidden');
  });

  it('starts with no chart expanded', () => {
    const service = new ChartModalService();
    expect(service.state()).toBeNull();
  });

  it('open() sets the state and locks page scroll', () => {
    const service = new ChartModalService();
    service.open('Revenue by Region', { series: [{ type: 'bar', data: [1, 2] }] }, 'Bar');

    expect(service.state()).toEqual({
      title: 'Revenue by Region',
      options: { series: [{ type: 'bar', data: [1, 2] }] },
      badge: 'Bar',
    });
    expect(document.body.classList.contains('overflow-hidden')).toBe(true);
  });

  it('open() without a badge leaves badge undefined', () => {
    const service = new ChartModalService();
    service.open('T', { series: [] });
    expect(service.state()?.badge).toBeUndefined();
  });

  it('close() clears the state and unlocks page scroll', () => {
    const service = new ChartModalService();
    service.open('T', { series: [] });
    service.close();

    expect(service.state()).toBeNull();
    expect(document.body.classList.contains('overflow-hidden')).toBe(false);
  });
});
