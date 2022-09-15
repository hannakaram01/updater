# updater
Autoupdater based on a xml reader
AUTOUPDATER RETAIL POS 1.0 - CANAIMA STORE

Funcionamiento Lógico (su programación):
	Verifica que existan parches locales (ruta ...c:/Temp[Modificable en el app.config]). Debe existir el UPDATEINFO.xml y el/los archivos zip.
	De haberlos, leera los respectivos xml y se realizará la acción y movimiento requeridos (Insert de la información procesada en la BD).
	Descarga del XML general, lectura del mismo y descarga individual de cada parche comprimido
	Unzip del parche, lectura xml de instrucciones, Insert de la información procesada en la BD y borrado de parches.zip descargados

Nota: Los archivos de actualización locales también serán borrados automaticamente.

CONFIGURACIÓN Y USO:
	Se debe EDITAR MANUALMENTE el archivo App.config con los datos de conexión a la base de datos, 
			terminal, dataareaid, ConnectionString, localUpdatePath y ServerUpdatePath.
        Ejecutar los scripts sql necesarios        :


			             =>  AxVersions
                      [Tablas]       =>  DetailPatchUpdate
			             =>  localFilesInfo

					     
					          => [GC_SP_localFilesInfo] 
                  [Procedimientos Almacenados]    => [GC_SP_AxVersions] 
					          => [GC_SP_DetailPatchUpdate] 
					          => [GC_SP_InsertAxVersions] 
					          => [GC_SP_UpdateAxVersions] 
	
	Crear apropiadamente el archivo XML General y específicos (cuidado especial en el nombre y la extensión).
        Pueden crearse de forma manual o empleando el programa CreateXml.

	El archivo XML debe poseer OBLIGATORIAMENTE los siguientes nodos: 

	<guidpatch>        </guidpatch>   => Código GUID del parche (debe ser igual al reflejado en el XML general)
        <filename>         </filename>    => Nombre del archivo (extensión incluida: ejemplo app.dll) [Nvarchar 200]
	<description>      </description>  => Describe los cambios aplicados al archivo, o la naturaleza de su inclusión al sistema.
        <versionfile>      </versionfile>  
        <versionproduct>   </versionproduct>    => caracteristicas de version en los archivos .dll. NA (No Aplica) a sql y otros.
        <creationdate>     </creationdate>      => Info del archivo en cuestión.
        <size>             </size>              => Tamaño que posea el archivo.
        <ext>              </ext>               => Extensión del archivo (incluyendo el . : ejemplo .dll).
        <codepath>         </codepath>          => Elegir códigos de ruta: dll, doc,config,desk,esp.

        Nodos OPCIONALES (únicamente para sustituir o eliminar archivos en rutas especificas:

        <pathdestiny>      </pathdestiny>  
        <removepath>       </removepath>  

Carpeta destino para archivos:

Con extensiones:

dll -> carpeta de extensiones del POS
doc -> carpeta MyDocuments
config -> carpeta local del programa ejecutable
desk -> carpeta lógica escritorio

esp-> Obligatorio Anexar la sección/NODO pathdestiny en el XML.

PARA REMOVER UN ARCHIVO agregar el NODO removepath, SOLO en el caso de que la extension no sea dll (por tanto la ruta no sea la carpeta extensions).
