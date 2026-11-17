# Holos.Foundation

Piezas reutilizables para aplicaciones sobre ASP.NET Boilerplate (`Abp.*`) que quitan código repetido.

Es una librería nueva, no un fork de ABP: se apoya en los puntos de extensión que ABP ya expone.

## Caja de herramientas

| Paquete | Herramientas | Cuándo usarlas |
| --- | --- | --- |
| `Holos.Foundation.Domain` | `IMustHaveCompany`, `IMayHaveCompany`, `IRequestsCompany`, `IHasCode`, `ILocalizableException`, `LocalizableDomainException` | Contratos de ámbito, códigos y errores de negocio sin dependencia de idiomas. |
| `Holos.Foundation.Application` | `CompanyCrudAppService`, `CodedCompanyCrudAppService`, `ICompanyAccessGuard`, `SequentialCodePolicy` | CRUD con autorización y filtro por compañía; códigos únicos y sugerencias. |
| `Holos.Foundation.Application` | `LocalizableExceptionInterceptor`, traductor y opciones | Traducir errores de dominio en la frontera del AppService. |
| `Holos.Foundation.Application` | `DisableCompanyFilter` | Consultas explícitas a compañías previamente autorizadas. |
| `Holos.Foundation.EntityFrameworkCore` | `CompanyScope`, `ICompanyFilterContext`, registro del filtro | Aplicar el ámbito a consultas, inserciones y modificaciones. |
| `Holos.Foundation.EntityFrameworkCore` | `ToPagedResultAsync`, `ToPagedProjectionAsync` | Paginar consultas ordenadas; mapear en memoria o proyectar columnas en SQL. |
| `Holos.Foundation.Architecture` | `CompanyFilterGuard`, `LiteralMessageGuard`, `LocalizationDictionaryGuard` | Guardas de pruebas para configuración y localización. |
| Domain / EntityFrameworkCore | `VersionedSeedRunner`, `IVersionedSeedStore`, `RelationalVersionedSeedStore` | Ejecutar versiones de datos con un adaptador explícito de historial y transacciones. |

Los paquetes se apoyan en ASP.NET Boilerplate (`Abp.*`). No son módulos autoensamblables:
la aplicación registra autorización, contexto y localización. Los ejemplos usan los nombres
actuales de los paquetes; los antiguos `Holos.Foundation.Abp.*` no son los nombres actuales.

## Compañía activa

**Compañía activa implícita**, igual que ABP hace con el inquilino.

- Las entidades implementan `IMustHaveCompany` (`long CompanyId`) o `IMayHaveCompany` (`long? CompanyId`).
- Toda consulta se filtra por la compañía activa; no hace falta `Where(x => x.CompanyId == ...)`.
- Al insertar, `CompanyId` se completa con la compañía activa. Con el filtro activo, una compañía distinta se rechaza.
- Sin compañía activa y con el filtro activo, las consultas no devuelven filas (cierra por defecto).

### Uso

1. Registrar el filtro en `PreInitialize` del módulo: `Configuration.UnitOfWork.RegisterCompanyFilter();`
2. En el `DbContext`, implementar `ICompanyFilterContext` y delegar los filtros y conceptos de escritura en `CompanyScope`.
   Ejemplo compilado: `test/.../AbpIntegrationExample.cs`.
3. Aplicar también `CompanyScope.ApplyOnModified` en el punto de extensión de modificaciones
   de ABP para rechazar el traslado de filas entre compañías.
4. Revisar **cada contexto realmente usado por un repositorio**, incluidos repositorios explícitos.
   Declarar un `DbSet` en otro contexto no hace que ese repositorio adopte sus filtros.
5. Para una operación de una compañía, establecer su contexto desde una fuente autorizada y
   guardar antes de restaurarlo. En jobs, autorizar al solicitante antes de leer datos.
6. Para un consolidado: autorizar las compañías primero y desactivar solo el filtro de compañía
   durante la consulta, manteniendo el predicado explícito de tenant y compañías autorizadas:

```csharp
using (uow.Current.DisableCompanyFilter())
{
    rows = await repository.GetAll()
        .Where(x => x.TenantId == tenantId && authorizedCompanyIds.Contains(x.CompanyId))
        .ToListAsync(cancellationToken);
}
```

No usar `IgnoreQueryFilters()` como sustituto: puede quitar también aislamiento de tenant y
soft-delete. Desactivar el filtro no autoriza el acceso ni prepara un contexto de escritura.

## CRUD, códigos y auditoría

Derivar de `CompanyCrudAppService<TEntity, TDto, TKey, TList, TCreate, TUpdate>` e inyectar
repositorio e `ICompanyAccessGuard` en `base(repository, companyAccess)`. Las entidades implementan
`IMustHaveCompany`; los DTO con compañía solicitada implementan `IRequestsCompany`. El guard es
responsabilidad del producto: debe comprobar compañía activa y acceso del usuario.

Configurar `GetPermissionName`, `GetAllPermissionName`, `CreatePermissionName`, `UpdatePermissionName`
y `DeletePermissionName` según el contrato del producto. Usar `LoadEntityAsync` si se requieren
relaciones (`Include`), conservando el control previo de compañía. Una fila no visible produce
`EntityNotFoundException`; la aplicación comprueba su contrato HTTP mediante pruebas.

Usar `CodedCompanyCrudAppService` cuando entidad y DTO de entrada implementen `IHasCode`.
Definir `CodePrefix` y `DuplicateCodeMessageKey`; la clase concentra comprobación de duplicados
y `SuggestCodeAsync`. La base protege auditoría de creación al actualizar mediante su mapeador;
un override propio que no lo invoque debe conservar esa garantía.

La comprobación de unicidad por consulta no reemplaza un índice único ni resuelve por sí sola
dos inserciones simultáneas. Verificar además normalización y la política de códigos eliminados
del producto antes de adoptar la base.

## Errores localizados

```csharp
throw new LocalizableDomainException("ProductCodeAlreadyExists", code);
```

Registrar `LocalizableExceptionInterceptor.Register(iocManager, localizationSourceName)` antes
de registrar los AppServices. La aplicación proporciona todos los diccionarios y sus traducciones.
El interceptor transforma las excepciones que implementan `ILocalizableException` en
`UserFriendlyException`; no convierte una clave ausente en una traducción correcta.

## Paginación

Partir de una consulta **ya filtrada, autorizada y ordenada de forma determinista**, preferiblemente
con Id como desempate. `ToPagedResultAsync` recibe un delegado de mapeo y materializa entidades;
`ToPagedProjectionAsync` recibe una expresión traducible a SQL. Ambos leen sin seguimiento.
No sustituir mapeos calculados o relaciones sin verificar campos, orden, totales y JSON resultante.

## Guardas

- `CompanyFilterGuard.FindEntitiesWithoutDbSet(contextType, domainAssemblies)` detecta entidades
  marcadas que faltan en el contexto indicado. **No certifica el contexto resuelto por cada repositorio.**
- `CompanyFilterGuard.DiscoverAssemblies` permite recorrer ensamblados de dominio; revisar el filtro
  de ensamblados para el producto concreto.
- `LiteralMessageGuard.FindLiteralMessages(sourceRoot)` encuentra las formas de literal soportadas
  por su patrón. No es un analizador semántico completo de C#.
- `LocalizationDictionaryGuard`: cargar XML y comprobar claves ausentes, duplicadas y marcadores.
  No certifica calidad lingüística ni todas las formas posibles del formato compuesto de .NET.

Las guardas devuelven incidencias: el proyecto de pruebas debe comprobar que estén vacías.

## Seeds versionados

Ver [contrato, transacciones y límites del ejecutor](src/Holos.Foundation.EntityFrameworkCore/Seeding/README.md).
Foundation no decide qué campos puede editar un usuario ni qué catálogo pertenece al sistema.
Esa política vive en el dominio del producto y debe aplicarse también a la API.

Para catálogos: congelar las entradas de versiones publicadas, usar un SeedId nuevo para ampliaciones,
preservar valores personalizados y eliminados, y guardar acción e historial juntos. Crear un catálogo
ausente, añadir ítems y corregir datos obligatorios son operaciones distintas. No reutilizar un upsert
completo para distribuir ítems nuevos si puede sobrescribir personalizaciones.

### Estado

- Los ejemplos y tests están en [el proyecto de pruebas](test/Holos.Foundation.EntityFrameworkCore.Tests).
- El ejemplo de `AbpDbContext` comprueba firmas por compilación; no debe confundirse con un E2E.
- Hay pruebas relacionales SQLite de seeds y concurrencia. No certifican un adaptador PostgreSQL
  ni la integración transaccional del aprovisionamiento de un producto.
- Ejecutar `dotnet test Holos.Foundation.sln --no-restore` para validar el estado local. Los resultados
  históricos no certifican una compilación, configuración o versión de paquete distinta.
