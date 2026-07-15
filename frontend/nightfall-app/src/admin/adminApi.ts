const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
let csrf = "";

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${baseUrl}/api/admin${path}`, { ...init, credentials: "include", headers: { "Content-Type": "application/json", ...(csrf ? { "X-CSRF-Token": csrf } : {}), ...init.headers } });
  if (!response.ok) { const body = await response.json().catch(() => ({})); throw new Error(body.detail ?? `Request failed (${response.status})`); }
  return response.status === 204 ? (undefined as T) : response.json();
}

export const adminApi = {
  login: (username: string, password: string) => request<{username:string}>("/auth/login", { method:"POST", body:JSON.stringify({username,password}) }),
  session: () => request<{authenticated:boolean;username:string}>("/auth/session"),
  csrf: async () => { const r=await request<{token:string}>("/auth/csrf"); csrf=r.token; },
  logout: () => request<void>("/auth/logout", {method:"POST"}),
  get: <T>(path:string) => request<T>(path),
  put: <T>(path:string, body:unknown) => request<T>(path,{method:"PUT",body:JSON.stringify(body)}),
  post: <T>(path:string, body:unknown) => request<T>(path,{method:"POST",body:JSON.stringify(body)}),
};
