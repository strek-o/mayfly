import http from "k6/http";
import { check, fail, sleep } from "k6";

const baseUrl = __ENV.BASE_URL;
const authCookieName = ".AspNetCore.Identity.Application";
const shareLinkPermitLimit = 30;

export const options = {
  scenarios: {
    owner: {
      executor: "ramping-vus",
      exec: "owner",
      stages: [
        { duration: "30s", target: 10 },
        { duration: "1m", target: 10 },
        { duration: "10s", target: 0 },
      ],
    },
    share_link_limit: {
      executor: "per-vu-iterations",
      exec: "shareLinkLimit",
      vus: 1,
      iterations: 1,
      startTime: "45s",
    },
  },
  thresholds: {
    "http_req_duration{scenario:owner}": ["p(95)<1000"],
    "http_req_failed{scenario:owner}": ["rate<0.01"],
    "checks{scenario:share_link_limit}": ["rate==1"],
  },
};

export function setup() {
  const loginPage = http.get(`${baseUrl}/account/login`);
  const antiforgery = loginPage
    .html()
    .find('input[name="__RequestVerificationToken"]')
    .attr("value");

  http.post(`${baseUrl}/account/login`, {
    _handler: "login",
    __RequestVerificationToken: antiforgery,
    "Input.Email": __ENV.MAYFLY_EMAIL,
    "Input.Password": __ENV.MAYFLY_PASSWORD,
  });

  const authCookie = http.cookieJar().cookiesForURL(baseUrl)[authCookieName];
  if (!authCookie) {
    fail("Sign in failed: check MAYFLY_EMAIL and MAYFLY_PASSWORD.");
  }

  return { authCookie: authCookie[0] };
}

export function owner(data) {
  const params = {
    headers: { Cookie: `${authCookieName}=${data.authCookie}` },
    redirects: 0,
  };

  const pastes = http.get(`${baseUrl}/pastes`, params);
  check(pastes, { "pastes page renders": (r) => r.status === 200 });

  const download = http.get(
    `${baseUrl}/files/${__ENV.FILE_ID}/download`,
    params,
  );
  check(download, {
    "download redirects to a sas url": (r) =>
      r.status === 302 && r.headers["Location"].includes("sig="),
  });

  sleep(1);
}

export function shareLinkLimit() {
  const statuses = [];
  for (let i = 0; i <= shareLinkPermitLimit + 10; i++) {
    const response = http.get(`${baseUrl}/s/${__ENV.SHARE_TOKEN}`, {
      headers: { "X-Forwarded-For": `203.0.113.${i}` },
    });
    statuses.push(response.status);
  }

  check(statuses, {
    "share link opens before the limit": (s) => s[0] === 200,
    "spoofed client addresses still hit the limit": (s) => s.includes(429),
    "no server errors": (s) => s.every((status) => status < 500),
  });
}
