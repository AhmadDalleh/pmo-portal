import { HttpParams } from '@angular/common/http';

/** Query string from an object. Empty, null and undefined values are left out, so "no filter" is simply absent. */
export function toParams(values: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
