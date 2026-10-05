"""
로컬 개발 서버 실행 스크립트.

Windows에서는 uvicorn이 기본적으로 asyncio.ProactorEventLoop를 강제로 사용하는데,
psycopg의 비동기 커넥션 풀은 SelectorEventLoop가 필요해서 둘이 충돌한다
(uvicorn/loops/asyncio.py 참고 — reload 또는 workers>1일 때만 SelectorEventLoop로 전환됨).
그래서 reload=True로 실행해 이 경로를 강제로 타게 한다.
"""

import uvicorn

if __name__ == "__main__":
    uvicorn.run("main:app", host="127.0.0.1", port=8000, reload=True)
