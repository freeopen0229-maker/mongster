"""Runnable login/API scaffold. A reviewed game adapter is required for game endpoints."""
import os
from pathlib import Path
from typing import Literal
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel, ConfigDict, Field
from .auth_store import AuthStore
from .providers import ProviderVerifier, InvalidIdentity, ProviderUnavailable
from .game_gateway import DomainUnavailable

class EmailLogin(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)
    email: str = Field(min_length=3,max_length=254)
    password: str = Field(min_length=12,max_length=128)

class SocialLogin(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)
    provider: Literal['google','kakao']
    token: str = Field(min_length=1,max_length=16384)

class PetCommand(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)
    PetId: str = Field(pattern=r'^[A-Za-z0-9_-]{1,80}$')
    RequestId: str = Field(pattern=r'^[A-Za-z0-9_-]{1,80}$')
    ExpectedRevision: int = Field(ge=0)
    Action: int = Field(ge=0,le=2)

class EconomyCommand(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)
    requestId: str = Field(pattern=r'^[A-Za-z0-9_-]{1,80}$')
    kind: int = Field(ge=0,le=4)
    petId: str | None = Field(default=None,pattern=r'^(?:[A-Za-z0-9_-]{1,80})?$')
    tenPack: bool = False
    expectedRevision: int = Field(default=0,ge=0)
    slotIndex: int = Field(default=0,ge=0)

def create_app(store, verifier, game=None):
    app = FastAPI(docs_url=None,redoc_url=None,openapi_url=None)

    @app.exception_handler(RequestValidationError)
    async def invalid_body(request, exc):
        # Default validation details can echo passwords/provider tokens back to the caller.
        return JSONResponse({'error':'invalid_request'},status_code=400,headers={'Cache-Control':'no-store'})

    @app.middleware('http')
    async def no_cache(request, call_next):
        response = await call_next(request)
        response.headers['Cache-Control'] = 'no-store'
        return response

    def throttle(request, identity=''):
        host = request.client.host if request.client else 'unknown'
        if not store.allow_attempt('ip:'+host) or (identity and not store.allow_attempt('identity:'+identity,10)):
            raise HTTPException(429,'Too many attempts')

    def account(request):
        header = request.headers.get('authorization','')
        token = header[7:] if header.startswith('Bearer ') else ''
        owner = store.account_for(token)
        if not owner:
            raise HTTPException(401,'Authentication required')
        return owner, token

    def domain(owner, action, selected=None, command=None):
        if game is None:
            raise HTTPException(503,'Game domain adapter not configured')
        try:
            code, body = game.handle(owner,action,selected,command)
        except DomainUnavailable:
            raise HTTPException(503,'Game account/world not provisioned')
        return JSONResponse(body,status_code=code)

    def login_result(owner):
        try:
            selected = game.selected_pet(owner) if game is not None else ''
        except DomainUnavailable:
            raise HTTPException(503,'Game account/world not provisioned')
        return dict(accountId=owner,accessToken=store.issue(owner),selectedPetId=selected)

    @app.get('/healthz')
    def health():
        return {'status':'ok','gameAdapterConfigured':game is not None}

    @app.post('/v2/auth/email/login')
    def email_login(body:EmailLogin, request:Request):
        throttle(request,body.email.strip().lower())
        try:
            owner = store.email_account(body.email,body.password)
        except ValueError:
            owner = None
        if not owner:
            raise HTTPException(401,'Invalid credentials')
        return login_result(owner)

    @app.post('/v2/auth/social/login')
    def social_login(body:SocialLogin, request:Request):
        throttle(request)
        try:
            subject = verifier.verify(body.provider,body.token)
        except InvalidIdentity:
            raise HTTPException(401,'Invalid provider token')
        except ProviderUnavailable:
            raise HTTPException(503,'Identity provider unavailable')
        return login_result(store.social_account(body.provider,subject))

    @app.post('/v2/auth/logout')
    def logout(request:Request):
        _, token = account(request)
        store.revoke(token)
        return {'ok':True}

    @app.get('/v2/client/snapshot')
    def snapshot(request:Request,petId:str|None=None):
        owner,_ = account(request)
        return domain(owner,'read',petId)

    @app.post('/v2/client/command')
    def economy_command(body:EconomyCommand,request:Request):
        owner,_ = account(request)
        return domain(owner,'command',command=body.model_dump())

    @app.get('/v2/pets/snapshot')
    def pet_snapshot(request:Request,petId:str):
        owner,_ = account(request)
        return domain(owner,'pet-read',petId)

    def add_pet_route(path, action):
        def run(body:PetCommand,request:Request):
            owner,_ = account(request)
            return domain(owner,action,command=body.model_dump())
        app.post(path)(run)
    for path,action in [('care','care'),('claim-idle','claim'),('exploration/start','start'),('exploration/finish','finish')]:
        add_pet_route('/v2/pets/'+path,'pet-'+action)
    return app

def create_default_app():
    path = Path(os.environ.get('MONGSTER_AUTH_DB','/var/lib/mongster/auth.sqlite'))
    # Deployment creates this directory with service-user-only access.
    store = AuthStore(path)
    verifier = ProviderVerifier(os.environ.get('GOOGLE_WEB_CLIENT_ID',''),os.environ.get('KAKAO_APP_ID',''))
    return create_app(store,verifier)
