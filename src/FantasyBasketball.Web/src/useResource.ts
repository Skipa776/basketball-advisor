import { useCallback, useEffect, useState } from 'react';
import { api, message } from './api';
import type { Envelope } from './api';

export function useResource<T>(path: string | null, interval = 0) {
  const [revision, setRevision] = useState(0);
  const [state, setState] = useState<{ path: string | null; result: Envelope<T> | null; error: string; loading: boolean }>({ path: null, result: null, error: '', loading: true });
  const refresh = useCallback(() => setRevision(value => value + 1), []);
  useEffect(() => {
    if (!path) return;
    const controller = new AbortController();
    setState(previous => ({ path, result: previous.path === path ? previous.result : null, error: '', loading: true }));
    api<T>(path, { signal: controller.signal }).then(result => {
      if (!controller.signal.aborted) setState({ path, result, error: '', loading: false });
    }).catch(error => {
      if (!controller.signal.aborted) setState({ path, result: null, error: message(error), loading: false });
    });
    return () => controller.abort();
  }, [path, revision]);
  useEffect(() => {
    if (!interval || !path) return;
    const poll = () => { if (!document.hidden) refresh(); };
    const timer = window.setInterval(poll, interval);
    window.addEventListener('focus', poll);
    return () => { clearInterval(timer); window.removeEventListener('focus', poll); };
  }, [interval, path, refresh]);
  return { result: state.path === path ? state.result : null, error: state.path === path ? state.error : '', loading: !!path && (state.path !== path || state.loading), refresh };
}
