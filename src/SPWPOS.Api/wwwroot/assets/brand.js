import {esc} from './ui.js';

export const brand=(href='/',extra='',label='PLANORA — Plan smarter. Work realistically.')=>`<a class="brand brand-lockup ${esc(extra)}" href="${esc(href)}" aria-label="${esc(label)}"><img class="brand-image" src="/assets/planora-brand.png" width="2158" height="729" alt="PLANORA — Plan smarter. Work realistically." decoding="async"></a>`;
