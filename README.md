# Holos.Foundation

Piezas reutilizables para aplicaciones sobre ASP.NET Boilerplate (`Abp.*`) que quitan código repetido.

Es una librería nueva, no un fork de ABP: se apoya en los puntos de extensión que ABP ya expone.

## Holos.Foundation.Abp.EntityFrameworkCore

**Compañía activa implícita**, igual que ABP hace con el inquilino.

- Las entidades implementan `IMustHaveCompany` (`long CompanyId`) o `IMayHaveCompany` (`long? CompanyId`).
- Toda consulta se filtra por la compañía activa; no hace falta `Where(x => x.CompanyId == ...)`.
- Al insertar, `CompanyId` se completa con la compañía activa. Con el filtro activo, una compañía distinta se rechaza.
- Sin compañía activa y con el filtro activo, las consultas no devuelven filas (cierra por defecto).

### Uso

1. Registrar el filtro en `PreInitialize` del módulo: `Configuration.UnitOfWork.RegisterCompanyFilter();`
2. En el `DbContext`, implementar `ICompanyFilterContext` y delegar tres métodos en `CompanyScope`.
   Ejemplo compilado: `test/.../AbpIntegrationExample.cs`.
3. Procesos sin compañía (jobs, seeders, host): `using (uow.Current.DisableFilter(CompanyFilter.Name)) { ... }`.

### Estado

- Verificado: filtro por instancia de contexto, compañía opcional, asignación e rechazo en inserciones (8 pruebas con SQLite).
- El ejemplo de `AbpDbContext` solo se compila contra Abp.EntityFrameworkCore 10.3.0; **no se ha ejecutado dentro de una aplicación ABP**.
- Pendiente: probar en un módulo real de HolosCoreOps, y varias compañías por consulta (`companyIds`).
