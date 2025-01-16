# HTTP Function Lambda

## Setup
### Variables de entorno
- Nombre de la tabla

`TABLA_RECLAMOS=reclamo-enre-xxx`
- GUID de control/validacion para pegarle a los endpoints `start` y `stop`

`CODIGO_VALIDACION=00000000-0000-0000-0000-000000000000`
- Agregar el ARN del Schedule de EventBridge (te lo da Pulumi)

`SCHEDULE_ARN=arn:aws:scheduler:us-east-1:0000000000:schedule/enre-xxxxx/enre-reclamo-xxxxx`

### appSettings.Development.json
- Configurar el nombre de tu profile de AWS en `appSettings.Development.json` 
```
  "AWS": {
    "Profile": "mi-perfil",
    "Region": "us-east-1"
  },
```