export interface ConsoleStatus { initialized: boolean; brandName: string; botConfigured?: boolean; }
export interface ConsoleUser { username: string; role: string; brandName: string; }
export interface ConsoleBot { id: string; name: string; address?: string | null; status: string; }
export interface TrackResource { type: string; resid: string; title?: string; add?: { [key: string]: string }; }
export interface Track { resource: TrackResource; title: string; type: string; coverUrl?: string; active: boolean; }
export interface MusicState {
  configured: boolean;
  connected: boolean;
  current: Track | null;
  paused?: boolean;
  position?: number;
  length?: number;
  volume?: number;
  /** off | one | all */
  loop?: string;
  random?: boolean;
  queue: Track[];
  recent: Track[];
}
export async function consoleApi<T>(path: string, body?: object): Promise<T> { const response=await fetch(`/console-api/${path}`,{method:body?"POST":"GET",credentials:"same-origin",headers:body?{"Content-Type":"application/json"}:undefined,body:body?JSON.stringify(body):undefined}); let json:any={}; try{json=await response.json();}catch(_){} if(!response.ok)throw new Error(json.error||"请求失败。");return json as T; }
