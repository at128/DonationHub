import base64
import argparse
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser(description='Run API checks against a new, isolated SQL Server database (Windows integrated authentication).')
parser.add_argument('--server', default='localhost')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[1]
run_id = uuid.uuid4().hex[:10]
database = 'DonationHub_ApiVerify_' + run_id
connection = f'Server={args.server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;'
env = os.environ.copy()
password = 'Aa9!' + uuid.uuid4().hex
admin_email = f'admin-{run_id}@example.test'
donor_email = f'donor-{run_id}@example.test'
env.update({
    'ASPNETCORE_ENVIRONMENT': 'Development',
    'DOTNET_ENVIRONMENT': 'Development',
    'ConnectionStrings__DefaultConnection': connection,
    'Jwt__SigningKey': base64.b64encode(os.urandom(64)).decode(),
    'AdminSeed__Email': admin_email,
    'AdminSeed__Password': password,
    'SeedAdmin__Email': admin_email,
    'SeedAdmin__Password': password,
    'Donation__MinAmount': '1.00',
    'Donation__MaxAmount': '10000.00',
    'Donation__PendingTimeoutMinutes': '30',
    'Donation__Currency': 'USD',
    'DevelopmentSeed__ManagerEmail': f'demo-manager-{run_id}@example.test',
    'DevelopmentSeed__ManagerPassword': password,
    'DevelopmentSeed__DonorEmail': f'demo-donor-{run_id}@example.test',
    'DevelopmentSeed__DonorPassword': password,
    'Logging__LogLevel__Default': 'Warning',
    'Logging__LogLevel__Microsoft': 'Warning',
})
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
base = f'http://127.0.0.1:{port}'
checks = []

def check(name, condition):
    if not condition:
        raise AssertionError(name)
    checks.append(name)
    print('PASS ' + name, flush=True)

def http(method, path, body=None, token=None):
    headers = {}
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers['Content-Type'] = 'application/json'
    if token:
        headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(base + path, data=data, method=method, headers=headers)
    try:
        response = urllib.request.urlopen(req, timeout=15)
    except urllib.error.HTTPError as ex:
        response = ex
    raw = response.read().decode()
    try:
        value = json.loads(raw) if raw else None
    except json.JSONDecodeError:
        value = raw
    return response.status, value, response.headers

def sql(command):
    script = (
        "$ErrorActionPreference = 'Stop'\n"
        "$conn = New-Object System.Data.SqlClient.SqlConnection('" + connection.replace("'", "''") + "')\n"
        "$conn.Open()\ntry {\n$cmd = $conn.CreateCommand()\n"
        "$cmd.CommandText = @'\n" + command + "\n'@\n"
        "$result = $cmd.ExecuteScalar()\nif ($null -ne $result) { Write-Output $result }\n"
        "} finally { $conn.Dispose() }"
    )
    result = subprocess.run(['powershell', '-NoProfile', '-Command', script],
                            capture_output=True, text=True, check=True)
    return result.stdout.strip()

print('TEST DATABASE ' + database, flush=True)
migration = subprocess.run([
    'dotnet', 'ef', 'database', 'update', 'InitialCreate', '--no-build', '--project', 'DonationHub.Infrastructure',
    '--startup-project', 'DonationHub.Api', '--connection', connection,
], cwd=repo, env=env, capture_output=True, text=True)
if migration.returncode:
    print(migration.stdout)
    print(migration.stderr)
    raise SystemExit(migration.returncode)
legacy_category = str(uuid.uuid4())
legacy_campaign = str(uuid.uuid4())
legacy_user = str(uuid.uuid4())
sql(f"""INSERT INTO AspNetUsers (Id, FullName, PreferredLanguage, IsActive, CreatedAt, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
VALUES ('{legacy_user}', N'Legacy', 'en', 1, GETUTCDATE(), 0, 0, 0, 0, 0);
INSERT INTO Categories (Id, NameAr, NameEn, DescriptionAr, DescriptionEn, CreatedAt, UpdatedAt)
VALUES ('{legacy_category}', N'legacy', N'Legacy', N'legacy', N'Legacy', GETUTCDATE(), GETUTCDATE());
INSERT INTO Campaigns (Id, GoalAmount, RaisedAmount, EndDate, Status, TitleAr, TitleEn, DescriptionAr, DescriptionEn, CategoryId, CreatedByUserId, IsDeleted, CreatedAt, UpdatedAt)
VALUES ('{legacy_campaign}', 100, 0, DATEADD(day, 10, GETUTCDATE()), 'Active', N'legacy', N'Legacy', N'legacy', N'Legacy', '{legacy_category}', '{legacy_user}', 1, GETUTCDATE(), GETUTCDATE());""")
missing_creator = str(uuid.uuid4())
sql(f"UPDATE Campaigns SET CreatedByUserId = '{missing_creator}' WHERE Id = '{legacy_campaign}';")
migration = subprocess.run(['dotnet', 'ef', 'database', 'update', '--no-build', '--project', 'DonationHub.Infrastructure',
    '--startup-project', 'DonationHub.Api', '--connection', connection], cwd=repo, env=env, capture_output=True, text=True)
check('creator migration rejects orphan references with a clear error', migration.returncode != 0 and
    'Campaigns contain creator references with no matching user' in migration.stdout + migration.stderr)
check('failed creator migration retains orphan row for explicit repair', sql(
    f"SELECT COUNT(*) FROM Campaigns WHERE Id = '{legacy_campaign}' AND CreatedByUserId = '{missing_creator}';") == '1')
sql(f"UPDATE Campaigns SET CreatedByUserId = '{legacy_user}' WHERE Id = '{legacy_campaign}';")
migration = subprocess.run(['dotnet', 'ef', 'database', 'update', '--no-build', '--project', 'DonationHub.Infrastructure',
    '--startup-project', 'DonationHub.Api', '--connection', connection], cwd=repo, env=env, capture_output=True, text=True)
if migration.returncode:
    print(migration.stdout)
    print(migration.stderr)
    raise SystemExit(migration.returncode)
check('migration preserves existing row and backfills deletion timestamp', sql(
    f"SELECT COUNT(*) FROM Campaigns WHERE Id = '{legacy_campaign}' AND IsDeleted = 1 AND DeletedAtUtc = UpdatedAt;") == '1')
log_path = Path(tempfile.gettempdir()) / f'donationhub-cleanup-{run_id}.log'
with log_path.open('w', encoding='utf-8') as log:
    process = subprocess.Popen([
        str(repo / 'DonationHub.Api/bin/Debug/net10.0/DonationHub.Api.exe'),
        '--contentRoot', str(repo / 'DonationHub.Api'), '--urls', base,
    ], cwd=repo, env=env, stdout=log, stderr=subprocess.STDOUT)
    try:
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError('API exited during startup; log: ' + str(log_path))
            try:
                if http('GET', '/')[0] == 200:
                    break
            except OSError:
                pass
            time.sleep(.25)
        else:
            raise RuntimeError('API startup timeout; log: ' + str(log_path))

        unknown = str(uuid.uuid4())
        category = {'nameAr': 'فحص الصحة', 'nameEn': 'Test Health', 'descriptionAr': 'دعم الصحة', 'descriptionEn': 'Healthcare'}
        status, initial_categories, _ = http('GET', '/api/v1/categories')
        check('public category list and five bilingual development seeds', status == 200 and len(initial_categories) == 6 and
            {c['nameEn'] for c in initial_categories}.issuperset({'Health', 'Education', 'Relief', 'Orphans', 'Environment'}) and
            all(c['nameAr'] for c in initial_categories))
        status, value, _ = http('GET', '/api/v1/categories/' + unknown)
        check('missing category has stable error code', status == 404 and value['code'] == 'Category.NotFound')
        check('anonymous category mutation rejected', http('POST', '/api/v1/categories', category)[0] == 401)

        status, admin, _ = http('POST', '/api/v1/auth/login', {'email': admin_email, 'password': password})
        check('seeded admin login and token contract', status == 200 and admin['expiresIn'] == 900 and admin['tokenType'] == 'Bearer')
        admin_token = admin['accessToken']
        for label in ('Manager', 'Donor'):
            status, demo_tokens, _ = http('POST', '/api/v1/auth/login', {'email': env[f'DevelopmentSeed__{label}Email'], 'password': password})
            expected_role = 'CampaignManager' if label == 'Manager' else 'Donor'
            check(f'development {label} seed can log in with correct role', status == 200 and
                http('GET', '/api/v1/auth/me', token=demo_tokens['accessToken'])[1]['roles'] == [expected_role])
        profile = {'email': donor_email, 'password': password, 'fullName': '  Test Donor  ', 'preferredLanguage': 'ar'}
        status, donor, _ = http('POST', '/api/v1/auth/register', profile)
        check('donor registration keeps response shape', status == 201 and set(donor) == {'id', 'email', 'fullName'} and donor['fullName'] == 'Test Donor')
        status, value, _ = http('POST', '/api/v1/auth/register', dict(profile, email=donor_email.upper()))
        check('case-insensitive duplicate email', status == 409 and value['code'] == 'Auth.EmailAlreadyExists')
        check('invalid profile rejected', http('POST', '/api/v1/auth/register', dict(profile, fullName=''))[0] == 400)
        check('weak password rejected', http('POST', '/api/v1/auth/register', dict(profile, email=f'weak-{run_id}@example.test', password='abc'))[0] == 400)
        wrong_password = http('POST', '/api/v1/auth/login', {'email': donor_email, 'password': 'Wrong9Password'})
        wrong_email = http('POST', '/api/v1/auth/login', {'email': f'absent-{run_id}@example.test', 'password': 'Wrong9Password'})
        check('wrong email and password return the same response', wrong_password[0:2] == wrong_email[0:2] and wrong_email[0] == 401)
        status, donor_tokens, _ = http('POST', '/api/v1/auth/login', {'email': donor_email, 'password': password})
        check('donor login', status == 200)
        donor_token = donor_tokens['accessToken']
        status, me, _ = http('GET', '/api/v1/auth/me', token=donor_token)
        check('current profile and Donor role', status == 200 and me['roles'] == ['Donor'] and me['preferredLanguage'] == 'ar')
        check('donor category mutation forbidden', http('POST', '/api/v1/categories', category, donor_token)[0] == 403)
        staff_request = {'email': f'manager-{run_id}@example.test', 'temporaryPassword': password, 'fullName': 'Test Manager', 'preferredLanguage': 'en', 'role': 'CampaignManager'}
        check('staff requires Admin role', http('POST', '/api/v1/admin/staff', staff_request, donor_token)[0] == 403)
        check('invalid staff role rejected', http('POST', '/api/v1/admin/staff', dict(staff_request, role='Donor'), admin_token)[0] == 400)
        status, staff, _ = http('POST', '/api/v1/admin/staff', staff_request, admin_token)
        check('staff creation keeps role and response shape', status == 201 and staff['role'] == 'CampaignManager' and set(staff) == {'id', 'email', 'fullName', 'role'})
        check('staff duplicate email rejected', http('POST', '/api/v1/admin/staff', staff_request, admin_token)[0] == 409)
        check('staff uses shared profile validation', http('POST', '/api/v1/admin/staff', dict(staff_request, fullName=''), admin_token)[0] == 400)

        status, created, headers = http('POST', '/api/v1/categories', category, admin_token)
        check('category create returns DTO and Location', status == 201 and set(created) == {'id', 'nameAr', 'nameEn', 'descriptionAr', 'descriptionEn'} and headers['Location'].endswith(created['id']))
        path = '/api/v1/categories/' + created['id']
        check('public detail DTO', http('GET', path)[0:2] == (200, created))
        check('public category list includes created DTO', created in http('GET', '/api/v1/categories')[1])
        check('independent English name uniqueness, case-insensitive', http('POST', '/api/v1/categories', dict(category, nameAr='اسم آخر', nameEn='test health'), admin_token)[0] == 409)
        check('independent Arabic name uniqueness', http('POST', '/api/v1/categories', dict(category, nameEn='Another'), admin_token)[0] == 409)
        check('blank Arabic name validation', http('POST', '/api/v1/categories', dict(category, nameAr=' '), admin_token)[0] == 400)
        check('name maximum length validation', http('POST', '/api/v1/categories', dict(category, nameEn='x' * 101), admin_token)[0] == 400)
        check('missing category update', http('PUT', '/api/v1/categories/' + unknown, category, admin_token)[0] == 404)
        updated = dict(category, nameAr='فحص الصحة العامة', nameEn='Test Public Health')
        status, value, _ = http('PUT', path, updated, admin_token)
        check('category update', status == 200 and value['nameEn'] == updated['nameEn'])
        check('category update permits its own names', http('PUT', path, updated, admin_token)[0] == 200)
        status, another, _ = http('POST', '/api/v1/categories', dict(category, nameAr='فحص التعليم', nameEn='Test Education'), admin_token)
        assert status == 201
        check('category update rejects another category name', http('PUT', path, dict(updated, nameEn='Test Education'), admin_token)[0] == 409)
        check('category delete', http('DELETE', path, token=admin_token)[0] == 204)
        check('deleted category is absent', http('GET', path)[0] == 404)
        check('missing category delete', http('DELETE', path, token=admin_token)[0] == 404)

        campaign_id = str(uuid.uuid4())
        sql(f"""INSERT INTO Campaigns (Id, GoalAmount, RaisedAmount, EndDate, Status, TitleAr, TitleEn, DescriptionAr, DescriptionEn, CategoryId, CreatedByUserId, IsDeleted, CreatedAt, UpdatedAt)
VALUES ('{campaign_id}', 100, 0, DATEADD(day, 10, GETUTCDATE()), 'Active', N'test', N'test', N'test', N'test', '{another['id']}', '{donor['id']}', 0, GETUTCDATE(), GETUTCDATE());""")
        restricted_path = '/api/v1/categories/' + another['id']
        status, value, _ = http('DELETE', restricted_path, token=admin_token)
        check('domain deletion rule blocks linked campaign', status == 409 and value['code'] == 'Category.HasCampaigns')
        sql(f"UPDATE Campaigns SET IsDeleted = 1 WHERE Id = '{campaign_id}';")
        status, value, _ = http('DELETE', restricted_path, token=admin_token)
        check('soft-deleted campaign also blocks category deletion', status == 409 and value['code'] == 'Category.HasCampaigns')

        status, rotated, _ = http('POST', '/api/v1/auth/refresh', {'refreshToken': donor_tokens['refreshToken']})
        check('refresh rotation', status == 200 and rotated['refreshToken'] != donor_tokens['refreshToken'])
        check('refresh replay rejected', http('POST', '/api/v1/auth/refresh', {'refreshToken': donor_tokens['refreshToken']})[0] == 401)
        old_hash = hashlib.sha256(donor_tokens['refreshToken'].encode()).hexdigest().upper()
        check('refresh stored hashed with revocation and replacement link', sql(f"SELECT COUNT(*) FROM RefreshTokens WHERE TokenHash = '{old_hash}' AND RevokedAt IS NOT NULL AND ReplacedByTokenId IS NOT NULL;") == '1')
        check('logout', http('POST', '/api/v1/auth/logout', {'refreshToken': rotated['refreshToken']}, rotated['accessToken'])[0] == 204)
        check('logged-out refresh rejected', http('POST', '/api/v1/auth/refresh', {'refreshToken': rotated['refreshToken']})[0] == 401)

        status, disabled_tokens, _ = http('POST', '/api/v1/auth/login', {'email': donor_email, 'password': password})
        assert status == 200
        sql(f"UPDATE AspNetUsers SET IsActive = 0 WHERE Id = '{donor['id']}';")
        status, value, _ = http('POST', '/api/v1/auth/login', {'email': donor_email, 'password': password})
        check('disabled login has required 403 code', status == 403 and value['code'] == 'Auth.AccountDeactivated')
        status, value, _ = http('POST', '/api/v1/auth/login', {'email': donor_email, 'password': 'wrong'})
        check('disabled wrong password remains generic', status == 401 and value['code'] == 'Auth.InvalidCredentials')
        status, value, _ = http('POST', '/api/v1/auth/refresh', {'refreshToken': disabled_tokens['refreshToken']})
        check('disabled refresh has required 403 code', status == 403 and value['code'] == 'Auth.AccountDeactivated')
        sql(f"UPDATE AspNetUsers SET IsActive = 1 WHERE Id = '{donor['id']}';")

        for iteration in range(3):
            status, race_tokens, _ = http('POST', '/api/v1/auth/login', {'email': donor_email, 'password': password})
            assert status == 200
            before = int(sql('SELECT COUNT(*) FROM RefreshTokens;'))
            with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
                attempts = list(executor.map(lambda _: http('POST', '/api/v1/auth/refresh', {'refreshToken': race_tokens['refreshToken']}), range(2)))
            check(f'concurrent refresh attempt {iteration + 1} has exactly one winner', sorted(a[0] for a in attempts) == [200, 401] and
                next(a[1] for a in attempts if a[0] == 401)['code'] == 'Auth.RefreshTokenInvalid')
            check(f'concurrent refresh attempt {iteration + 1} leaves no orphan replacement', int(sql('SELECT COUNT(*) FROM RefreshTokens;')) == before + 1)

        race_profile = dict(profile, email=f'race-{run_id}@example.test')
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            attempts = list(executor.map(lambda _: http('POST', '/api/v1/auth/register', race_profile), range(2)))
        check('concurrent duplicate registration returns 201 and 409', sorted(a[0] for a in attempts) == [201, 409] and
            next(a[1] for a in attempts if a[0] == 409)['code'] == 'Auth.EmailAlreadyExists')

        status, expired_tokens, _ = http('POST', '/api/v1/auth/login', {'email': donor_email, 'password': password})
        assert status == 200
        expired_hash = hashlib.sha256(expired_tokens['refreshToken'].encode()).hexdigest().upper()
        sql(f"UPDATE RefreshTokens SET ExpiresAt = DATEADD(second, -1, GETUTCDATE()) WHERE TokenHash = '{expired_hash}';")
        status, value, _ = http('POST', '/api/v1/auth/refresh', {'refreshToken': expired_tokens['refreshToken']})
        check('expired refresh returns required 401 code', status == 401 and value['code'] == 'Auth.RefreshTokenInvalid')

        status, document, _ = http('GET', '/openapi/v1.json')
        check('OpenAPI and Bearer scheme', status == 200 and document['components']['securitySchemes']['Bearer']['scheme'] == 'bearer')
        check('OpenAPI category mutation declares security', bool(document['paths']['/api/v1/categories']['post']['security']))
        check('Scalar UI responds', http('GET', '/scalar/v1')[0] == 200)
        check('original and additive migrations applied', sql('SELECT COUNT(*) FROM __EFMigrationsHistory;') == '3')
        snapshot = sql('SELECT CONCAT((SELECT COUNT(*) FROM AspNetUsers), \'/\', (SELECT COUNT(*) FROM Categories), \'/\', (SELECT COUNT(*) FROM AspNetRoles));')
        process.terminate()
        process.wait(timeout=10)
        process = subprocess.Popen([str(repo / 'DonationHub.Api/bin/Debug/net10.0/DonationHub.Api.exe'),
            '--contentRoot', str(repo / 'DonationHub.Api'), '--urls', base], cwd=repo, env=env, stdout=log, stderr=subprocess.STDOUT)
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError('Restart failed; log: ' + str(log_path))
            try:
                if http('GET', '/')[0] == 200:
                    break
            except OSError:
                pass
            time.sleep(.25)
        else:
            raise RuntimeError('Restart timeout')
        check('repeated startup does not duplicate seed data', snapshot == sql('SELECT CONCAT((SELECT COUNT(*) FROM AspNetUsers), \'/\', (SELECT COUNT(*) FROM Categories), \'/\', (SELECT COUNT(*) FROM AspNetRoles));'))
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()

# Production seeding is checked on a second fresh database, without demo credentials.
database = 'DonationHub_ProductionVerify_' + run_id
connection = f'Server={args.server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;'
env['ASPNETCORE_ENVIRONMENT'] = env['DOTNET_ENVIRONMENT'] = 'Production'
env['ConnectionStrings__DefaultConnection'] = connection
for key in list(env):
    if key.startswith('DevelopmentSeed__'):
        del env[key]
migration = subprocess.run(['dotnet', 'ef', 'database', 'update', '--no-build', '--project', 'DonationHub.Infrastructure',
    '--startup-project', 'DonationHub.Api', '--connection', connection], cwd=repo, env=env, capture_output=True, text=True)
if migration.returncode:
    raise RuntimeError('Production test migration failed: ' + migration.stderr)
with log_path.open('a', encoding='utf-8') as log:
    process = subprocess.Popen([str(repo / 'DonationHub.Api/bin/Debug/net10.0/DonationHub.Api.exe'),
        '--contentRoot', str(repo / 'DonationHub.Api'), '--urls', base], cwd=repo, env=env, stdout=log, stderr=subprocess.STDOUT)
    try:
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError('Production startup failed; log: ' + str(log_path))
            try:
                if http('GET', '/')[0] == 200:
                    break
            except OSError:
                pass
            time.sleep(.25)
        else:
            raise RuntimeError('Production startup timeout')
        check('Production seeds only one Admin and the three roles', sql('SELECT COUNT(*) FROM AspNetUsers;') == '1' and sql('SELECT COUNT(*) FROM AspNetRoles;') == '3')
        check('Production does not seed demo categories', sql('SELECT COUNT(*) FROM Categories;') == '0')
        check('Production does not expose OpenAPI', http('GET', '/openapi/v1.json')[0] == 404)
    finally:
        process.terminate()
        process.wait(timeout=10)

for key, value in [('Jwt__SigningKey', 'invalid-base64'), ('Donation__MaxAmount', '0.5')]:
    invalid_env = dict(env)
    invalid_env[key] = value
    invalid_env['ConnectionStrings__DefaultConnection'] = 'Server=127.0.0.1,1;Database=MustNotConnect;Integrated Security=True;Connect Timeout=1'
    startup = subprocess.run([str(repo / 'DonationHub.Api/bin/Debug/net10.0/DonationHub.Api.exe'), '--contentRoot', str(repo / 'DonationHub.Api')],
        cwd=repo, env=invalid_env, capture_output=True, text=True, timeout=15)
    output = startup.stdout + startup.stderr
    check(f'invalid {key} fails before database access', startup.returncode != 0 and 'OptionsValidationException' in output and 'SqlException' not in output)
print(f'RESULT {len(checks)}/{len(checks)} checks passed. Isolated test databases retained with suffix {run_id}.', flush=True)
