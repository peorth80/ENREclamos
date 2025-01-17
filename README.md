# ENREClamos

## Que es
Estuve un mes sin luz. Me habia cansado de hacer los reclamos, y aparte a la noche no podia hacerlos, asi que decidi hacer una app que lo haga por mi cada 4 horas. Guarda los resultados de los reclamos en un bucket y aparte una tabla para ver los numeros de reclamo de forma facil. Costo, practicamente 0 (si estas en Free Tier, es 0, literal)

## Stack
- AWS 
- C#
- Pulumi

### Lambda 1: ENREClamos
Este es el lambda que se llama desde EventBridge. Tiene un modo `dry_run` para probar sin que haga el POST al ENRE, guarda la entrada en Dynamo, pero no guarda el HTML del bucket. 

### Lambda 2: Ver reclamos
Desde el HTTP Function, con `http://[endpoint]/list` podemos ver un JSON con la lista de los reclamos, en que fecha se hicieron, y si fueron hechos en `DryRun` mode o no
El scheduler tambien se puede arrancar `http://[endpoint]/start/<guid>` o detener `http://[endpoint]/stop/<guid>`. Guid es un Guid de control que se define en Pulumi, un layer de seguridad para pobres.

## Como instalarlo
`infra` crea toda la infraestructura necesaria. 

1. Configurar la cuenta de AWS, poner el perfil que corresponda en el `pulumi.dev.yml`, configurar nro medidor, nro cliente, etc.
2. Compilar las apps (correr `./build.sh` en los proyectos que estan en `src`)
3. `pulumi up`
4. Una vez que termina de crear todo, revisar como quedo en AWS. Va a crear:
- 1 bucket
- 1 tabla de dynamo
- 1 Schedule en Eventbride
- 2 functiones lambda (1 de ellas con function URL)
- Logs de Cloudwatch
- Roles y permisos
5. Se puede ir a Lambda functions -> Test y poner `{}` como Event message. Recuerden ponerlo en `dry_run` si quieren probarlo, para no pegarle al site real
6. La function URL se puede probar con `http://[endpoint]/list`

## Tests
Los tests son pobres y estan hechos asi nomas. Para lo que lo necesitaba era suficiente

## PR
Si tenes ganas, manda, para lo que lo necesitaba me sirve.

## Soporta otra cosa que el ENRE?
Si tu empresa de energia tiene una pagina que podes mandar los reclamos, podes adaptarlo a lo que necesites. 
