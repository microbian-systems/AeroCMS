//! Licensed to the .NET Foundation under one or more agreements.
//! The .NET Foundation licenses this file to you under the MIT license.

var e=!1;const t=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,4,1,96,0,0,3,2,1,0,10,8,1,6,0,6,64,25,11,11])),o=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,15,1,13,0,65,1,253,15,65,2,253,15,253,128,2,11])),n=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,10,1,8,0,65,0,253,15,253,98,11])),r=Symbol.for("wasm promise_control");function i(e,t){let o=null;const n=new Promise((function(n,r){o={isDone:!1,promise:null,resolve:t=>{o.isDone||(o.isDone=!0,n(t),e&&e())},reject:e=>{o.isDone||(o.isDone=!0,r(e),t&&t())}}}));o.promise=n;const i=n;return i[r]=o,{promise:i,promise_control:o}}function s(e){return e[r]}function a(e){e&&function(e){return void 0!==e[r]}(e)||Be(!1,"Promise is not controllable")}const l="__mono_message__",c=["debug","log","trace","warn","info","error"],d="MONO_WASM: ";let u,f,m,g,p,h;function w(e){g=e}function b(e){if(Pe.diagnosticTracing){const t="function"==typeof e?e():e;console.debug(d+t)}}function y(e,...t){console.info(d+e,...t)}function v(e,...t){console.info(e,...t)}function E(e,...t){console.warn(d+e,...t)}function _(e,...t){if(t&&t.length>0&&t[0]&&"object"==typeof t[0]){if(t[0].silent)return;if(t[0].toString)return void console.error(d+e,t[0].toString())}console.error(d+e,...t)}function x(e,t,o){return function(...n){try{let r=n[0];if(void 0===r)r="undefined";else if(null===r)r="null";else if("function"==typeof r)r=r.toString();else if("string"!=typeof r)try{r=JSON.stringify(r)}catch(e){r=r.toString()}t(o?JSON.stringify({method:e,payload:r,arguments:n.slice(1)}):[e+r,...n.slice(1)])}catch(e){m.error(`proxyConsole failed: ${e}`)}}}function j(e,t,o){f=t,g=e,m={...t};const n=`${o}/console`.replace("https://","wss://").replace("http://","ws://");u=new WebSocket(n),u.addEventListener("error",A),u.addEventListener("close",S),function(){for(const e of c)f[e]=x(`console.${e}`,T,!0)}()}function R(e){let t=30;const o=()=>{u?0==u.bufferedAmount||0==t?(e&&v(e),function(){for(const e of c)f[e]=x(`console.${e}`,m.log,!1)}(),u.removeEventListener("error",A),u.removeEventListener("close",S),u.close(1e3,e),u=void 0):(t--,globalThis.setTimeout(o,100)):e&&m&&m.log(e)};o()}function T(e){u&&u.readyState===WebSocket.OPEN?u.send(e):m.log(e)}function A(e){m.error(`[${g}] proxy console websocket error: ${e}`,e)}function S(e){m.debug(`[${g}] proxy console websocket closed: ${e}`,e)}function D(){Pe.preferredIcuAsset=O(Pe.config);let e="invariant"==Pe.config.globalizationMode;if(!e)if(Pe.preferredIcuAsset)Pe.diagnosticTracing&&b("ICU data archive(s) available, disabling invariant mode");else{if("custom"===Pe.config.globalizationMode||"all"===Pe.config.globalizationMode||"sharded"===Pe.config.globalizationMode){const e="invariant globalization mode is inactive and no ICU data archives are available";throw _(`ERROR: ${e}`),new Error(e)}Pe.diagnosticTracing&&b("ICU data archive(s) not available, using invariant globalization mode"),e=!0,Pe.preferredIcuAsset=null}const t="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",o=Pe.config.environmentVariables;if(void 0===o[t]&&e&&(o[t]="1"),void 0===o.TZ)try{const e=Intl.DateTimeFormat().resolvedOptions().timeZone||null;e&&(o.TZ=e)}catch(e){y("failed to detect timezone, will fallback to UTC")}}function O(e){var t;if((null===(t=e.resources)||void 0===t?void 0:t.icu)&&"invariant"!=e.globalizationMode){const t=e.applicationCulture||(ke?globalThis.navigator&&globalThis.navigator.languages&&globalThis.navigator.languages[0]:Intl.DateTimeFormat().resolvedOptions().locale),o=e.resources.icu;let n=null;if("custom"===e.globalizationMode){if(o.length>=1)return o[0].name}else t&&"all"!==e.globalizationMode?"sharded"===e.globalizationMode&&(n=function(e){const t=e.split("-")[0];return"en"===t||["fr","fr-FR","it","it-IT","de","de-DE","es","es-ES"].includes(e)?"icudt_EFIGS.dat":["zh","ko","ja"].includes(t)?"icudt_CJK.dat":"icudt_no_CJK.dat"}(t)):n="icudt.dat";if(n)for(let e=0;e<o.length;e++){const t=o[e];if(t.virtualPath===n)return t.name}}return e.globalizationMode="invariant",null}(new Date).valueOf();const C=class{constructor(e){this.url=e}toString(){return this.url}};async function k(e,t){try{const o="function"==typeof globalThis.fetch;if(Se){const n=e.startsWith("file://");if(!n&&o)return globalThis.fetch(e,t||{credentials:"same-origin"});p||(h=Ne.require("url"),p=Ne.require("fs")),n&&(e=h.fileURLToPath(e));const r=await p.promises.readFile(e);return{ok:!0,headers:{length:0,get:()=>null},url:e,arrayBuffer:()=>r,json:()=>JSON.parse(r),text:()=>{throw new Error("NotImplementedException")}}}if(o)return globalThis.fetch(e,t||{credentials:"same-origin"});if("function"==typeof read)return{ok:!0,url:e,headers:{length:0,get:()=>null},arrayBuffer:()=>new Uint8Array(read(e,"binary")),json:()=>JSON.parse(read(e,"utf8")),text:()=>read(e,"utf8")}}catch(t){return{ok:!1,url:e,status:500,headers:{length:0,get:()=>null},statusText:"ERR28: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t},text:()=>{throw t}}}throw new Error("No fetch implementation available")}function I(e){return"string"!=typeof e&&Be(!1,"url must be a string"),!M(e)&&0!==e.indexOf("./")&&0!==e.indexOf("../")&&globalThis.URL&&globalThis.document&&globalThis.document.baseURI&&(e=new URL(e,globalThis.document.baseURI).toString()),e}const U=/^[a-zA-Z][a-zA-Z\d+\-.]*?:\/\//,P=/[a-zA-Z]:[\\/]/;function M(e){return Se||Ie?e.startsWith("/")||e.startsWith("\\")||-1!==e.indexOf("///")||P.test(e):U.test(e)}let L,N=0;const $=[],z=[],W=new Map,F={"js-module-threads":!0,"js-module-runtime":!0,"js-module-dotnet":!0,"js-module-native":!0,"js-module-diagnostics":!0},B={...F,"js-module-library-initializer":!0},V={...F,dotnetwasm:!0,heap:!0,manifest:!0},q={...B,manifest:!0},H={...B,dotnetwasm:!0},J={dotnetwasm:!0,symbols:!0},Z={...B,dotnetwasm:!0,symbols:!0},Q={symbols:!0};function G(e){return!("icu"==e.behavior&&e.name!=Pe.preferredIcuAsset)}function K(e,t,o){null!=t||(t=[]),Be(1==t.length,`Expect to have one ${o} asset in resources`);const n=t[0];return n.behavior=o,X(n),e.push(n),n}function X(e){V[e.behavior]&&W.set(e.behavior,e)}function Y(e){Be(V[e],`Unknown single asset behavior ${e}`);const t=W.get(e);if(t&&!t.resolvedUrl)if(t.resolvedUrl=Pe.locateFile(t.name),F[t.behavior]){const e=ge(t);e?("string"!=typeof e&&Be(!1,"loadBootResource response for 'dotnetjs' type should be a URL string"),t.resolvedUrl=e):t.resolvedUrl=ce(t.resolvedUrl,t.behavior)}else if("dotnetwasm"!==t.behavior)throw new Error(`Unknown single asset behavior ${e}`);return t}function ee(e){const t=Y(e);return Be(t,`Single asset for ${e} not found`),t}let te=!1;async function oe(){if(!te){te=!0,Pe.diagnosticTracing&&b("mono_download_assets");try{const e=[],t=[],o=(e,t)=>{!Z[e.behavior]&&G(e)&&Pe.expected_instantiated_assets_count++,!H[e.behavior]&&G(e)&&(Pe.expected_downloaded_assets_count++,t.push(se(e)))};for(const t of $)o(t,e);for(const e of z)o(e,t);Pe.allDownloadsQueued.promise_control.resolve(),Promise.all([...e,...t]).then((()=>{Pe.allDownloadsFinished.promise_control.resolve()})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e})),await Pe.runtimeModuleLoaded.promise;const n=async e=>{const t=await e;if(t.buffer){if(!Z[t.behavior]){t.buffer&&"object"==typeof t.buffer||Be(!1,"asset buffer must be array-like or buffer-like or promise of these"),"string"!=typeof t.resolvedUrl&&Be(!1,"resolvedUrl must be string");const e=t.resolvedUrl,o=await t.buffer,n=new Uint8Array(o);pe(t),await Ue.beforeOnRuntimeInitialized.promise,Ue.instantiate_asset(t,e,n)}}else J[t.behavior]?("symbols"===t.behavior&&(await Ue.instantiate_symbols_asset(t),pe(t)),J[t.behavior]&&++Pe.actual_downloaded_assets_count):(t.isOptional||Be(!1,"Expected asset to have the downloaded buffer"),!H[t.behavior]&&G(t)&&Pe.expected_downloaded_assets_count--,!Z[t.behavior]&&G(t)&&Pe.expected_instantiated_assets_count--)},r=[],i=[];for(const t of e)r.push(n(t));for(const e of t)i.push(n(e));Promise.all(r).then((()=>{Ce||Ue.coreAssetsInMemory.promise_control.resolve()})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e})),Promise.all(i).then((async()=>{Ce||(await Ue.coreAssetsInMemory.promise,Ue.allAssetsInMemory.promise_control.resolve())})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e}))}catch(e){throw Pe.err("Error in mono_download_assets: "+e),e}}}let ne=!1;function re(){if(ne)return;ne=!0;const e=Pe.config,t=[];if(e.assets)for(const t of e.assets)"object"!=typeof t&&Be(!1,`asset must be object, it was ${typeof t} : ${t}`),"string"!=typeof t.behavior&&Be(!1,"asset behavior must be known string"),"string"!=typeof t.name&&Be(!1,"asset name must be string"),t.resolvedUrl&&"string"!=typeof t.resolvedUrl&&Be(!1,"asset resolvedUrl could be string"),t.hash&&"string"!=typeof t.hash&&Be(!1,"asset resolvedUrl could be string"),t.pendingDownload&&"object"!=typeof t.pendingDownload&&Be(!1,"asset pendingDownload could be object"),t.isCore?$.push(t):z.push(t),X(t);else if(e.resources){const o=e.resources;o.wasmNative||Be(!1,"resources.wasmNative must be defined"),o.jsModuleNative||Be(!1,"resources.jsModuleNative must be defined"),o.jsModuleRuntime||Be(!1,"resources.jsModuleRuntime must be defined"),K(z,o.wasmNative,"dotnetwasm"),K(t,o.jsModuleNative,"js-module-native"),K(t,o.jsModuleRuntime,"js-module-runtime"),o.jsModuleDiagnostics&&K(t,o.jsModuleDiagnostics,"js-module-diagnostics");const n=(e,t,o)=>{const n=e;n.behavior=t,o?(n.isCore=!0,$.push(n)):z.push(n)};if(o.coreAssembly)for(let e=0;e<o.coreAssembly.length;e++)n(o.coreAssembly[e],"assembly",!0);if(o.assembly)for(let e=0;e<o.assembly.length;e++)n(o.assembly[e],"assembly",!o.coreAssembly);if(0!=e.debugLevel&&Pe.isDebuggingSupported()){if(o.corePdb)for(let e=0;e<o.corePdb.length;e++)n(o.corePdb[e],"pdb",!0);if(o.pdb)for(let e=0;e<o.pdb.length;e++)n(o.pdb[e],"pdb",!o.corePdb)}if(e.loadAllSatelliteResources&&o.satelliteResources)for(const e in o.satelliteResources)for(let t=0;t<o.satelliteResources[e].length;t++){const r=o.satelliteResources[e][t];r.culture=e,n(r,"resource",!o.coreAssembly)}if(o.coreVfs)for(let e=0;e<o.coreVfs.length;e++)n(o.coreVfs[e],"vfs",!0);if(o.vfs)for(let e=0;e<o.vfs.length;e++)n(o.vfs[e],"vfs",!o.coreVfs);const r=O(e);if(r&&o.icu)for(let e=0;e<o.icu.length;e++){const t=o.icu[e];t.name===r&&n(t,"icu",!1)}if(o.wasmSymbols)for(let e=0;e<o.wasmSymbols.length;e++)n(o.wasmSymbols[e],"symbols",!1)}if(e.appsettings)for(let t=0;t<e.appsettings.length;t++){const o=e.appsettings[t],n=he(o);"appsettings.json"!==n&&n!==`appsettings.${e.applicationEnvironment}.json`||z.push({name:o,behavior:"vfs",cache:"no-cache",useCredentials:!0})}e.assets=[...$,...z,...t]}async function ie(e){const t=await se(e);return await t.pendingDownloadInternal.response,t.buffer}async function se(e){try{return await ae(e)}catch(t){if(!Pe.enableDownloadRetry)throw t;if(Ie||Se)throw t;if(e.pendingDownload&&e.pendingDownloadInternal==e.pendingDownload)throw t;if(e.resolvedUrl&&-1!=e.resolvedUrl.indexOf("file://"))throw t;if(t&&404==t.status)throw t;e.pendingDownloadInternal=void 0,await Pe.allDownloadsQueued.promise;try{return Pe.diagnosticTracing&&b(`Retrying download '${e.name}'`),await ae(e)}catch(t){return e.pendingDownloadInternal=void 0,await new Promise((e=>globalThis.setTimeout(e,100))),Pe.diagnosticTracing&&b(`Retrying download (2) '${e.name}' after delay`),await ae(e)}}}async function ae(e){for(;L;)await L.promise;try{++N,N==Pe.maxParallelDownloads&&(Pe.diagnosticTracing&&b("Throttling further parallel downloads"),L=i());const t=await async function(e){if(e.pendingDownload&&(e.pendingDownloadInternal=e.pendingDownload),e.pendingDownloadInternal&&e.pendingDownloadInternal.response)return e.pendingDownloadInternal.response;if(e.buffer){const t=await e.buffer;return e.resolvedUrl||(e.resolvedUrl="undefined://"+e.name),e.pendingDownloadInternal={url:e.resolvedUrl,name:e.name,response:Promise.resolve({ok:!0,arrayBuffer:()=>t,json:()=>JSON.parse(new TextDecoder("utf-8").decode(t)),text:()=>{throw new Error("NotImplementedException")},headers:{get:()=>{}}})},e.pendingDownloadInternal.response}const t=e.loadRemote&&Pe.config.remoteSources?Pe.config.remoteSources:[""];let o;for(let n of t){n=n.trim(),"./"===n&&(n="");const t=le(e,n);e.name===t?Pe.diagnosticTracing&&b(`Attempting to download '${t}'`):Pe.diagnosticTracing&&b(`Attempting to download '${t}' for ${e.name}`);try{e.resolvedUrl=t;const n=fe(e);if(e.pendingDownloadInternal=n,o=await n.response,!o||!o.ok)continue;return o}catch(e){o||(o={ok:!1,url:t,status:0,statusText:""+e});continue}}const n=e.isOptional||e.name.match(/\.pdb$/)&&Pe.config.ignorePdbLoadErrors;if(o||Be(!1,`Response undefined ${e.name}`),!n){const t=new Error(`download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`);throw t.status=o.status,t}y(`optional download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`)}(e);return t?(J[e.behavior]||(e.buffer=await t.arrayBuffer(),++Pe.actual_downloaded_assets_count),e):e}finally{if(--N,L&&N==Pe.maxParallelDownloads-1){Pe.diagnosticTracing&&b("Resuming more parallel downloads");const e=L;L=void 0,e.promise_control.resolve()}}}function le(e,t){let o;return null==t&&Be(!1,`sourcePrefix must be provided for ${e.name}`),e.resolvedUrl?o=e.resolvedUrl:(o=""===t?"assembly"===e.behavior||"pdb"===e.behavior?e.name:"resource"===e.behavior&&e.culture&&""!==e.culture?`${e.culture}/${e.name}`:e.name:t+e.name,o=ce(Pe.locateFile(o),e.behavior)),o&&"string"==typeof o||Be(!1,"attemptUrl need to be path or url string"),o}function ce(e,t){return Pe.modulesUniqueQuery&&q[t]&&(e+=Pe.modulesUniqueQuery),e}let de=0;const ue=new Set;function fe(e){try{e.resolvedUrl||Be(!1,"Request's resolvedUrl must be set");const t=function(e){let t=e.resolvedUrl;if(Pe.loadBootResource){const o=ge(e);if(o instanceof Promise)return o;"string"==typeof o&&(t=o)}const o={};return e.cache?o.cache=e.cache:Pe.config.disableNoCacheFetch||(o.cache="no-cache"),e.useCredentials?o.credentials="include":!Pe.config.disableIntegrityCheck&&e.hash&&(o.integrity=e.hash),Pe.fetch_like(t,o)}(e),o={name:e.name,url:e.resolvedUrl,response:t};return ue.add(e.name),o.response.then((()=>{"assembly"==e.behavior&&Pe.loadedAssemblies.push(e.name),de++,Pe.onDownloadResourceProgress&&Pe.onDownloadResourceProgress(de,ue.size)})),o}catch(t){const o={ok:!1,url:e.resolvedUrl,status:500,statusText:"ERR29: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t}};return{name:e.name,url:e.resolvedUrl,response:Promise.resolve(o)}}}const me={resource:"assembly",assembly:"assembly",pdb:"pdb",icu:"globalization",vfs:"configuration",manifest:"manifest",dotnetwasm:"dotnetwasm","js-module-dotnet":"dotnetjs","js-module-native":"dotnetjs","js-module-runtime":"dotnetjs","js-module-threads":"dotnetjs"};function ge(e){var t;if(Pe.loadBootResource){const o=null!==(t=e.hash)&&void 0!==t?t:"",n=e.resolvedUrl,r=me[e.behavior];if(r){const t=Pe.loadBootResource(r,e.name,n,o,e.behavior);return"string"==typeof t?I(t):t}}}function pe(e){e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null}function he(e){let t=e.lastIndexOf("/");return t>=0&&t++,e.substring(t)}async function we(e){e&&await Promise.all((null!=e?e:[]).map((e=>async function(e){try{const t=e.name;if(!e.moduleExports){const o=ce(Pe.locateFile(t),"js-module-library-initializer");Pe.diagnosticTracing&&b(`Attempting to import '${o}' for ${e}`),e.moduleExports=await import(/*! webpackIgnore: true */o)}Pe.libraryInitializers.push({scriptName:t,exports:e.moduleExports})}catch(t){E(`Failed to import library initializer '${e}': ${t}`)}}(e))))}async function be(e,t){if(!Pe.libraryInitializers)return;const o=[];for(let n=0;n<Pe.libraryInitializers.length;n++){const r=Pe.libraryInitializers[n];r.exports[e]&&o.push(ye(r.scriptName,e,(()=>r.exports[e](...t))))}await Promise.all(o)}async function ye(e,t,o){try{await o()}catch(o){throw E(`Failed to invoke '${t}' on library initializer '${e}': ${o}`),Xe(1,o),o}}function ve(e,t){if(e===t)return e;const o={...t};return void 0!==o.assets&&o.assets!==e.assets&&(o.assets=[...e.assets||[],...o.assets||[]]),void 0!==o.resources&&(o.resources=_e(e.resources||{assembly:[],jsModuleNative:[],jsModuleRuntime:[],wasmNative:[]},o.resources)),void 0!==o.environmentVariables&&(o.environmentVariables={...e.environmentVariables||{},...o.environmentVariables||{}}),void 0!==o.runtimeOptions&&o.runtimeOptions!==e.runtimeOptions&&(o.runtimeOptions=[...e.runtimeOptions||[],...o.runtimeOptions||[]]),Object.assign(e,o)}function Ee(e,t){if(e===t)return e;const o={...t};return o.config&&(e.config||(e.config={}),o.config=ve(e.config,o.config)),Object.assign(e,o)}function _e(e,t){if(e===t)return e;const o={...t};return void 0!==o.coreAssembly&&(o.coreAssembly=[...e.coreAssembly||[],...o.coreAssembly||[]]),void 0!==o.assembly&&(o.assembly=[...e.assembly||[],...o.assembly||[]]),void 0!==o.lazyAssembly&&(o.lazyAssembly=[...e.lazyAssembly||[],...o.lazyAssembly||[]]),void 0!==o.corePdb&&(o.corePdb=[...e.corePdb||[],...o.corePdb||[]]),void 0!==o.pdb&&(o.pdb=[...e.pdb||[],...o.pdb||[]]),void 0!==o.jsModuleWorker&&(o.jsModuleWorker=[...e.jsModuleWorker||[],...o.jsModuleWorker||[]]),void 0!==o.jsModuleNative&&(o.jsModuleNative=[...e.jsModuleNative||[],...o.jsModuleNative||[]]),void 0!==o.jsModuleDiagnostics&&(o.jsModuleDiagnostics=[...e.jsModuleDiagnostics||[],...o.jsModuleDiagnostics||[]]),void 0!==o.jsModuleRuntime&&(o.jsModuleRuntime=[...e.jsModuleRuntime||[],...o.jsModuleRuntime||[]]),void 0!==o.wasmSymbols&&(o.wasmSymbols=[...e.wasmSymbols||[],...o.wasmSymbols||[]]),void 0!==o.wasmNative&&(o.wasmNative=[...e.wasmNative||[],...o.wasmNative||[]]),void 0!==o.icu&&(o.icu=[...e.icu||[],...o.icu||[]]),void 0!==o.satelliteResources&&(o.satelliteResources=function(e,t){if(e===t)return e;for(const o in t)e[o]=[...e[o]||[],...t[o]||[]];return e}(e.satelliteResources||{},o.satelliteResources||{})),void 0!==o.modulesAfterConfigLoaded&&(o.modulesAfterConfigLoaded=[...e.modulesAfterConfigLoaded||[],...o.modulesAfterConfigLoaded||[]]),void 0!==o.modulesAfterRuntimeReady&&(o.modulesAfterRuntimeReady=[...e.modulesAfterRuntimeReady||[],...o.modulesAfterRuntimeReady||[]]),void 0!==o.extensions&&(o.extensions={...e.extensions||{},...o.extensions||{}}),void 0!==o.vfs&&(o.vfs=[...e.vfs||[],...o.vfs||[]]),Object.assign(e,o)}function xe(){const e=Pe.config;if(e.environmentVariables=e.environmentVariables||{},e.runtimeOptions=e.runtimeOptions||[],e.resources=e.resources||{assembly:[],jsModuleNative:[],jsModuleWorker:[],jsModuleRuntime:[],wasmNative:[],vfs:[],satelliteResources:{}},e.assets){Pe.diagnosticTracing&&b("config.assets is deprecated, use config.resources instead");for(const t of e.assets){const o={};switch(t.behavior){case"assembly":o.assembly=[t];break;case"pdb":o.pdb=[t];break;case"resource":o.satelliteResources={},o.satelliteResources[t.culture]=[t];break;case"icu":o.icu=[t];break;case"symbols":o.wasmSymbols=[t];break;case"vfs":o.vfs=[t];break;case"dotnetwasm":o.wasmNative=[t];break;case"js-module-threads":o.jsModuleWorker=[t];break;case"js-module-runtime":o.jsModuleRuntime=[t];break;case"js-module-native":o.jsModuleNative=[t];break;case"js-module-diagnostics":o.jsModuleDiagnostics=[t];break;case"js-module-dotnet":break;default:throw new Error(`Unexpected behavior ${t.behavior} of asset ${t.name}`)}_e(e.resources,o)}}e.debugLevel,e.applicationEnvironment||(e.applicationEnvironment="Production"),e.applicationCulture&&(e.environmentVariables.LANG=`${e.applicationCulture}.UTF-8`),Ue.diagnosticTracing=Pe.diagnosticTracing=!!e.diagnosticTracing,Ue.waitForDebugger=e.waitForDebugger,Pe.maxParallelDownloads=e.maxParallelDownloads||Pe.maxParallelDownloads,Pe.enableDownloadRetry=void 0!==e.enableDownloadRetry?e.enableDownloadRetry:Pe.enableDownloadRetry}let je=!1;async function Re(e){var t;if(je)return void await Pe.afterConfigLoaded.promise;let o;try{if(e.configSrc||Pe.config&&0!==Object.keys(Pe.config).length&&(Pe.config.assets||Pe.config.resources)||(e.configSrc="dotnet.boot.js"),o=e.configSrc,je=!0,o&&(Pe.diagnosticTracing&&b("mono_wasm_load_config"),await async function(e){const t=e.configSrc,o=Pe.locateFile(t);let n=null;void 0!==Pe.loadBootResource&&(n=Pe.loadBootResource("manifest",t,o,"","manifest"));let r,i=null;if(n)if("string"==typeof n)n.includes(".json")?(i=await s(I(n)),r=await Ae(i)):r=(await import(I(n))).config;else{const e=await n;"function"==typeof e.json?(i=e,r=await Ae(i)):r=e.config}else o.includes(".json")?(i=await s(ce(o,"manifest")),r=await Ae(i)):r=(await import(ce(o,"manifest"))).config;function s(e){return Pe.fetch_like(e,{method:"GET",credentials:"include",cache:"no-cache"})}Pe.config.applicationEnvironment&&(r.applicationEnvironment=Pe.config.applicationEnvironment),ve(Pe.config,r)}(e)),xe(),await we(null===(t=Pe.config.resources)||void 0===t?void 0:t.modulesAfterConfigLoaded),await be("onRuntimeConfigLoaded",[Pe.config]),e.onConfigLoaded)try{await e.onConfigLoaded(Pe.config,Le),xe()}catch(e){throw _("onConfigLoaded() failed",e),e}xe(),Pe.afterConfigLoaded.promise_control.resolve(Pe.config)}catch(t){const n=`Failed to load config file ${o} ${t} ${null==t?void 0:t.stack}`;throw Pe.config=e.config=Object.assign(Pe.config,{message:n,error:t,isError:!0}),Xe(1,new Error(n)),t}}function Te(){return!!globalThis.navigator&&(Pe.isChromium||Pe.isFirefox)}async function Ae(e){const t=Pe.config,o=await e.json();t.applicationEnvironment||o.applicationEnvironment||(o.applicationEnvironment=e.headers.get("Blazor-Environment")||e.headers.get("DotNet-Environment")||void 0),o.environmentVariables||(o.environmentVariables={});const n=e.headers.get("DOTNET-MODIFIABLE-ASSEMBLIES");n&&(o.environmentVariables.DOTNET_MODIFIABLE_ASSEMBLIES=n);const r=e.headers.get("ASPNETCORE-BROWSER-TOOLS");return r&&(o.environmentVariables.__ASPNETCORE_BROWSER_TOOLS=r),o}"function"!=typeof importScripts||globalThis.onmessage||(globalThis.dotnetSidecar=!0);const Se="object"==typeof process&&"object"==typeof process.versions&&"string"==typeof process.versions.node,De="function"==typeof importScripts,Oe=De&&"undefined"!=typeof dotnetSidecar,Ce=De&&!Oe,ke="object"==typeof window||De&&!Se,Ie=!ke&&!Se;let Ue={},Pe={},Me={},Le={},Ne={},$e=!1;const ze={},We={config:ze},Fe={mono:{},binding:{},internal:Ne,module:We,loaderHelpers:Pe,runtimeHelpers:Ue,diagnosticHelpers:Me,api:Le};function Be(e,t){if(e)return;const o="Assert failed: "+("function"==typeof t?t():t),n=new Error(o);_(o,n),Ue.nativeAbort(n)}function Ve(){return void 0!==Pe.exitCode}function qe(){return Ue.runtimeReady&&!Ve()}function He(){Ve()&&Be(!1,`.NET runtime already exited with ${Pe.exitCode} ${Pe.exitReason}. You can use runtime.runMain() which doesn't exit the runtime.`),Ue.runtimeReady||Be(!1,".NET runtime didn't start yet. Please call dotnet.create() first.")}function Je(){ke&&(globalThis.addEventListener("unhandledrejection",et),globalThis.addEventListener("error",tt))}let Ze,Qe;function Ge(e){Qe&&Qe(e),Xe(e,Pe.exitReason)}function Ke(e){Ze&&Ze(e||Pe.exitReason),Xe(1,e||Pe.exitReason)}function Xe(t,o){var n,r;const i=o&&"object"==typeof o;t=i&&"number"==typeof o.status?o.status:void 0===t?-1:t;const s=i&&"string"==typeof o.message?o.message:""+o;(o=i?o:Ue.ExitStatus?function(e,t){const o=new Ue.ExitStatus(e);return o.message=t,o.toString=()=>t,o}(t,s):new Error("Exit with code "+t+" "+s)).status=t,o.message||(o.message=s);const a=""+(o.stack||(new Error).stack);try{Object.defineProperty(o,"stack",{get:()=>a})}catch(e){}const l=!!o.silent;if(o.silent=!0,Ve())Pe.diagnosticTracing&&b("mono_exit called after exit");else{try{We.onAbort==Ke&&(We.onAbort=Ze),We.onExit==Ge&&(We.onExit=Qe),ke&&(globalThis.removeEventListener("unhandledrejection",et),globalThis.removeEventListener("error",tt)),Ue.runtimeReady?(Ue.jiterpreter_dump_stats&&Ue.jiterpreter_dump_stats(!1),0===t&&(null===(n=Pe.config)||void 0===n?void 0:n.interopCleanupOnExit)&&Ue.forceDisposeProxies(!0,!0),e&&0!==t&&(null===(r=Pe.config)||void 0===r||r.dumpThreadsOnNonZeroExit)):(Pe.diagnosticTracing&&b(`abort_startup, reason: ${o}`),function(e){Pe.allDownloadsQueued.promise_control.reject(e),Pe.allDownloadsFinished.promise_control.reject(e),Pe.afterConfigLoaded.promise_control.reject(e),Pe.wasmCompilePromise.promise_control.reject(e),Pe.runtimeModuleLoaded.promise_control.reject(e),Ue.dotnetReady&&(Ue.dotnetReady.promise_control.reject(e),Ue.afterInstantiateWasm.promise_control.reject(e),Ue.beforePreInit.promise_control.reject(e),Ue.afterPreInit.promise_control.reject(e),Ue.afterPreRun.promise_control.reject(e),Ue.beforeOnRuntimeInitialized.promise_control.reject(e),Ue.afterOnRuntimeInitialized.promise_control.reject(e),Ue.afterPostRun.promise_control.reject(e))}(o))}catch(e){E("mono_exit A failed",e)}try{l||(function(e,t){if(0!==e&&t){const e=Ue.ExitStatus&&t instanceof Ue.ExitStatus?b:_;"string"==typeof t?e(t):(void 0===t.stack&&(t.stack=(new Error).stack+""),t.message?e(Ue.stringify_as_error_with_stack?Ue.stringify_as_error_with_stack(t.message+"\n"+t.stack):t.message+"\n"+t.stack):e(JSON.stringify(t)))}!Ce&&Pe.config&&(Pe.config.logExitCode?Pe.config.forwardConsoleLogsToWS?R("WASM EXIT "+e):v("WASM EXIT "+e):Pe.config.forwardConsoleLogsToWS&&R())}(t,o),function(e){if(ke&&!Ce&&Pe.config&&Pe.config.appendElementOnExit&&document){const t=document.createElement("label");t.id="tests_done",0!==e&&(t.style.background="red"),t.innerHTML=""+e,document.body.appendChild(t)}}(t))}catch(e){E("mono_exit B failed",e)}Pe.exitCode=t,Pe.exitReason||(Pe.exitReason=o),!Ce&&Ue.runtimeReady&&We.runtimeKeepalivePop()}if(Pe.config&&Pe.config.asyncFlushOnExit&&0===t)throw(async()=>{try{await async function(){try{const e=await import(/*! webpackIgnore: true */"process"),t=e=>new Promise(((t,o)=>{e.on("error",o),e.end("","utf8",t)})),o=t(e.stderr),n=t(e.stdout);let r;const i=new Promise((e=>{r=setTimeout((()=>e("timeout")),1e3)}));await Promise.race([Promise.all([n,o]),i]),clearTimeout(r)}catch(e){_(`flushing std* streams failed: ${e}`)}}()}finally{Ye(t,o)}})(),o;Ye(t,o)}function Ye(e,t){if(Ue.runtimeReady&&Ue.nativeExit)try{Ue.nativeExit(e)}catch(e){!Ue.ExitStatus||e instanceof Ue.ExitStatus||E("set_exit_code_and_quit_now failed: "+e.toString())}if(0!==e||!ke)throw Se&&Ne.process?Ne.process.exit(e):Ue.quit&&Ue.quit(e,t),t}function et(e){ot(e,e.reason,"rejection")}function tt(e){ot(e,e.error,"error")}function ot(e,t,o){e.preventDefault();try{t||(t=new Error("Unhandled "+o)),void 0===t.stack&&(t.stack=(new Error).stack),t.stack=t.stack+"",t.silent||(_("Unhandled error:",t),Xe(1,t))}catch(e){}}!function(e){if($e)throw new Error("Loader module already loaded");$e=!0,Ue=e.runtimeHelpers,Pe=e.loaderHelpers,Me=e.diagnosticHelpers,Le=e.api,Ne=e.internal,Object.assign(Le,{INTERNAL:Ne,invokeLibraryInitializers:be}),Object.assign(e.module,{config:ve(ze,{environmentVariables:{}})});const r={mono_wasm_bindings_is_ready:!1,config:e.module.config,diagnosticTracing:!1,nativeAbort:e=>{throw e||new Error("abort")},nativeExit:e=>{throw new Error("exit:"+e)}},l={gitHash:"901ca941248413c79832d2fdbd709da0c4386353",config:e.module.config,diagnosticTracing:!1,maxParallelDownloads:16,enableDownloadRetry:!0,_loaded_files:[],loadedFiles:[],loadedAssemblies:[],libraryInitializers:[],workerNextNumber:1,actual_downloaded_assets_count:0,actual_instantiated_assets_count:0,expected_downloaded_assets_count:0,expected_instantiated_assets_count:0,afterConfigLoaded:i(),allDownloadsQueued:i(),allDownloadsFinished:i(),wasmCompilePromise:i(),runtimeModuleLoaded:i(),loadingWorkers:i(),is_exited:Ve,is_runtime_running:qe,assert_runtime_running:He,mono_exit:Xe,createPromiseController:i,getPromiseController:s,assertIsControllablePromise:a,mono_download_assets:oe,resolve_single_asset_path:ee,setup_proxy_console:j,set_thread_prefix:w,installUnhandledErrorHandler:Je,retrieve_asset_download:ie,invokeLibraryInitializers:be,isDebuggingSupported:Te,exceptions:t,simd:n,relaxedSimd:o};Object.assign(Ue,r),Object.assign(Pe,l)}(Fe);let nt,rt,it,st=!1,at=!1;async function lt(e){if(!at){if(at=!0,ke&&Pe.config.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&j("main",globalThis.console,globalThis.location.origin),We||Be(!1,"Null moduleConfig"),Pe.config||Be(!1,"Null moduleConfig.config"),"function"==typeof e){const t=e(Fe.api);if(t.ready)throw new Error("Module.ready couldn't be redefined.");Object.assign(We,t),Ee(We,t)}else{if("object"!=typeof e)throw new Error("Can't use moduleFactory callback of createDotnetRuntime function.");Ee(We,e)}await async function(e){if(Se){const e=await import(/*! webpackIgnore: true */"process"),t=14;if(e.versions.node.split(".")[0]<t)throw new Error(`NodeJS at '${e.execPath}' has too low version '${e.versions.node}', please use at least ${t}. See also https://aka.ms/dotnet-wasm-features`)}const t=/*! webpackIgnore: true */import.meta.url,o=t.indexOf("?");var n;if(o>0&&(Pe.modulesUniqueQuery=t.substring(o)),Pe.scriptUrl=t.replace(/\\/g,"/").replace(/[?#].*/,""),Pe.scriptDirectory=(n=Pe.scriptUrl).slice(0,n.lastIndexOf("/"))+"/",Pe.locateFile=e=>"URL"in globalThis&&globalThis.URL!==C?new URL(e,Pe.scriptDirectory).toString():M(e)?e:Pe.scriptDirectory+e,Pe.fetch_like=k,Pe.out=console.log,Pe.err=console.error,Pe.onDownloadResourceProgress=e.onDownloadResourceProgress,ke&&globalThis.navigator){const e=globalThis.navigator,t=e.userAgentData&&e.userAgentData.brands;t&&t.length>0?Pe.isChromium=t.some((e=>"Google Chrome"===e.brand||"Microsoft Edge"===e.brand||"Chromium"===e.brand)):e.userAgent&&(Pe.isChromium=e.userAgent.includes("Chrome"),Pe.isFirefox=e.userAgent.includes("Firefox"))}Ne.require=Se?await import(/*! webpackIgnore: true */"module").then((e=>e.createRequire(/*! webpackIgnore: true */import.meta.url))):Promise.resolve((()=>{throw new Error("require not supported")})),void 0===globalThis.URL&&(globalThis.URL=C)}(We)}}async function ct(e){return await lt(e),Ze=We.onAbort,Qe=We.onExit,We.onAbort=Ke,We.onExit=Ge,We.ENVIRONMENT_IS_PTHREAD?async function(){(function(){const e=new MessageChannel,t=e.port1,o=e.port2;t.addEventListener("message",(e=>{var n,r;n=JSON.parse(e.data.config),r=JSON.parse(e.data.monoThreadInfo),st?Pe.diagnosticTracing&&b("mono config already received"):(ve(Pe.config,n),Ue.monoThreadInfo=r,xe(),Pe.diagnosticTracing&&b("mono config received"),st=!0,Pe.afterConfigLoaded.promise_control.resolve(Pe.config),ke&&n.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&Pe.setup_proxy_console("worker-idle",console,globalThis.location.origin)),t.close(),o.close()}),{once:!0}),t.start(),self.postMessage({[l]:{monoCmd:"preload",port:o}},[o])})(),await Pe.afterConfigLoaded.promise,function(){const e=Pe.config;e.assets||Be(!1,"config.assets must be defined");for(const t of e.assets)X(t),Q[t.behavior]&&z.push(t)}(),setTimeout((async()=>{try{await oe()}catch(e){Xe(1,e)}}),0);const e=dt(),t=await Promise.all(e);return await ut(t),We}():async function(){var e;await Re(We),re();const t=dt();(async function(){try{const e=ee("dotnetwasm");await se(e),e&&e.pendingDownloadInternal&&e.pendingDownloadInternal.response||Be(!1,"Can't load dotnet.native.wasm");const t=await e.pendingDownloadInternal.response,o=t.headers&&t.headers.get?t.headers.get("Content-Type"):void 0;let n;if("function"==typeof WebAssembly.compileStreaming&&"application/wasm"===o)n=await WebAssembly.compileStreaming(t);else{ke&&"application/wasm"!==o&&E('WebAssembly resource does not have the expected content type "application/wasm", so falling back to slower ArrayBuffer instantiation.');const e=await t.arrayBuffer();Pe.diagnosticTracing&&b("instantiate_wasm_module buffered"),n=Ie?await Promise.resolve(new WebAssembly.Module(e)):await WebAssembly.compile(e)}e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null,Pe.wasmCompilePromise.promise_control.resolve(n)}catch(e){Pe.wasmCompilePromise.promise_control.reject(e)}})(),setTimeout((async()=>{try{D(),await oe()}catch(e){Xe(1,e)}}),0);const o=await Promise.all(t);return await ut(o),await Ue.dotnetReady.promise,await we(null===(e=Pe.config.resources)||void 0===e?void 0:e.modulesAfterRuntimeReady),await be("onRuntimeReady",[Fe.api]),Le}()}function dt(){const e=ee("js-module-runtime"),t=ee("js-module-native");if(nt&&rt)return[nt,rt,it];"object"==typeof e.moduleExports?nt=e.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${e.resolvedUrl}' for ${e.name}`),nt=import(/*! webpackIgnore: true */e.resolvedUrl)),"object"==typeof t.moduleExports?rt=t.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${t.resolvedUrl}' for ${t.name}`),rt=import(/*! webpackIgnore: true */t.resolvedUrl));const o=Y("js-module-diagnostics");return o&&("object"==typeof o.moduleExports?it=o.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${o.resolvedUrl}' for ${o.name}`),it=import(/*! webpackIgnore: true */o.resolvedUrl))),[nt,rt,it]}async function ut(e){const{initializeExports:t,initializeReplacements:o,configureRuntimeStartup:n,configureEmscriptenStartup:r,configureWorkerStartup:i,setRuntimeGlobals:s,passEmscriptenInternals:a}=e[0],{default:l}=e[1],c=e[2];s(Fe),t(Fe),c&&c.setRuntimeGlobals(Fe),await n(We),Pe.runtimeModuleLoaded.promise_control.resolve(),l((e=>(Object.assign(We,{ready:e.ready,__dotnet_runtime:{initializeReplacements:o,configureEmscriptenStartup:r,configureWorkerStartup:i,passEmscriptenInternals:a}}),We))).catch((e=>{if(e.message&&e.message.toLowerCase().includes("out of memory"))throw new Error(".NET runtime has failed to start, because too much memory was requested. Please decrease the memory by adjusting EmccMaximumHeapSize. See also https://aka.ms/dotnet-wasm-features");throw e}))}const ft=new class{withModuleConfig(e){try{return Ee(We,e),this}catch(e){throw Xe(1,e),e}}withOnConfigLoaded(e){try{return Ee(We,{onConfigLoaded:e}),this}catch(e){throw Xe(1,e),e}}withConsoleForwarding(){try{return ve(ze,{forwardConsoleLogsToWS:!0}),this}catch(e){throw Xe(1,e),e}}withExitOnUnhandledError(){try{return ve(ze,{exitOnUnhandledError:!0}),Je(),this}catch(e){throw Xe(1,e),e}}withAsyncFlushOnExit(){try{return ve(ze,{asyncFlushOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withExitCodeLogging(){try{return ve(ze,{logExitCode:!0}),this}catch(e){throw Xe(1,e),e}}withElementOnExit(){try{return ve(ze,{appendElementOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withInteropCleanupOnExit(){try{return ve(ze,{interopCleanupOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withDumpThreadsOnNonZeroExit(){try{return ve(ze,{dumpThreadsOnNonZeroExit:!0}),this}catch(e){throw Xe(1,e),e}}withWaitingForDebugger(e){try{return ve(ze,{waitForDebugger:e}),this}catch(e){throw Xe(1,e),e}}withInterpreterPgo(e,t){try{return ve(ze,{interpreterPgo:e,interpreterPgoSaveDelay:t}),ze.runtimeOptions?ze.runtimeOptions.push("--interp-pgo-recording"):ze.runtimeOptions=["--interp-pgo-recording"],this}catch(e){throw Xe(1,e),e}}withConfig(e){try{return ve(ze,e),this}catch(e){throw Xe(1,e),e}}withConfigSrc(e){try{return e&&"string"==typeof e||Be(!1,"must be file path or URL"),Ee(We,{configSrc:e}),this}catch(e){throw Xe(1,e),e}}withVirtualWorkingDirectory(e){try{return e&&"string"==typeof e||Be(!1,"must be directory path"),ve(ze,{virtualWorkingDirectory:e}),this}catch(e){throw Xe(1,e),e}}withEnvironmentVariable(e,t){try{const o={};return o[e]=t,ve(ze,{environmentVariables:o}),this}catch(e){throw Xe(1,e),e}}withEnvironmentVariables(e){try{return e&&"object"==typeof e||Be(!1,"must be dictionary object"),ve(ze,{environmentVariables:e}),this}catch(e){throw Xe(1,e),e}}withDiagnosticTracing(e){try{return"boolean"!=typeof e&&Be(!1,"must be boolean"),ve(ze,{diagnosticTracing:e}),this}catch(e){throw Xe(1,e),e}}withDebugging(e){try{return null!=e&&"number"==typeof e||Be(!1,"must be number"),ve(ze,{debugLevel:e}),this}catch(e){throw Xe(1,e),e}}withApplicationArguments(...e){try{return e&&Array.isArray(e)||Be(!1,"must be array of strings"),ve(ze,{applicationArguments:e}),this}catch(e){throw Xe(1,e),e}}withRuntimeOptions(e){try{return e&&Array.isArray(e)||Be(!1,"must be array of strings"),ze.runtimeOptions?ze.runtimeOptions.push(...e):ze.runtimeOptions=e,this}catch(e){throw Xe(1,e),e}}withMainAssembly(e){try{return ve(ze,{mainAssemblyName:e}),this}catch(e){throw Xe(1,e),e}}withApplicationArgumentsFromQuery(){try{if(!globalThis.window)throw new Error("Missing window to the query parameters from");if(void 0===globalThis.URLSearchParams)throw new Error("URLSearchParams is supported");const e=new URLSearchParams(globalThis.window.location.search).getAll("arg");return this.withApplicationArguments(...e)}catch(e){throw Xe(1,e),e}}withApplicationEnvironment(e){try{return ve(ze,{applicationEnvironment:e}),this}catch(e){throw Xe(1,e),e}}withApplicationCulture(e){try{return ve(ze,{applicationCulture:e}),this}catch(e){throw Xe(1,e),e}}withResourceLoader(e){try{return Pe.loadBootResource=e,this}catch(e){throw Xe(1,e),e}}async download(){try{await async function(){lt(We),await Re(We),re(),D(),oe(),await Pe.allDownloadsFinished.promise}()}catch(e){throw Xe(1,e),e}}async create(){try{return this.instance||(this.instance=await async function(){return await ct(We),Fe.api}()),this.instance}catch(e){throw Xe(1,e),e}}async run(){try{return We.config||Be(!1,"Null moduleConfig.config"),this.instance||await this.create(),this.instance.runMainAndExit()}catch(e){throw Xe(1,e),e}}},mt=Xe,gt=ct;Ie||"function"==typeof globalThis.URL||Be(!1,"This browser/engine doesn't support URL API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),"function"!=typeof globalThis.BigInt64Array&&Be(!1,"This browser/engine doesn't support BigInt64Array API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),ft.withConfig(/*json-start*/{
  "mainAssemblyName": "Aero.Cms.Web.Client",
  "applicationEnvironment": "Development",
  "resources": {
    "hash": "sha256-bNr+sQ2JoWPTT5DcsDlz79JZeri8mUu1cxcIx5OtoNc=",
    "jsModuleNative": [
      {
        "name": "dotnet.native.ikrs475e5v.js"
      }
    ],
    "jsModuleRuntime": [
      {
        "name": "dotnet.runtime.a6jcqbs390.js"
      }
    ],
    "wasmNative": [
      {
        "name": "dotnet.native.veuqw8a0w9.wasm",
        "hash": "sha256-iQOJ2Ignl/X3n6mOHRQ4zWYcute0MGlaiRFi2J3HXWk=",
        "cache": "force-cache"
      }
    ],
    "icu": [
      {
        "virtualPath": "icudt.dat",
        "name": "icudt.oh1zvcfom8.dat",
        "hash": "sha256-tO5O5YzMTVSaKBboxAqezOQL9ewmupzV2JrB5Rkc8a4=",
        "cache": "force-cache"
      }
    ],
    "coreAssembly": [
      {
        "virtualPath": "System.Runtime.InteropServices.JavaScript.wasm",
        "name": "System.Runtime.InteropServices.JavaScript.a0smuvq74f.wasm",
        "hash": "sha256-tq5pToZJGO7SPoDjoI6ztMHmMkRlFnxE1syzsag+I2E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.CoreLib.wasm",
        "name": "System.Private.CoreLib.9s549gpy7m.wasm",
        "hash": "sha256-txDQUdiH3VmsOgqRUepjpm922YZKLTGi5aqjJYtcMTE=",
        "cache": "force-cache"
      }
    ],
    "assembly": [
      {
        "virtualPath": "AngleSharp.wasm",
        "name": "AngleSharp.lmcjrklybj.wasm",
        "hash": "sha256-rWHOVFc5oaArZN08nh3zGkFrMxu/qgjxSNJ+xpjfsTY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "AngleSharp.Css.wasm",
        "name": "AngleSharp.Css.wwsq4crpi6.wasm",
        "hash": "sha256-4Diue02BcmgMCs2GbdIncDbObwTBOLgvd6XPH0wuoiE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Azure.Core.wasm",
        "name": "Azure.Core.2ybcx8p94w.wasm",
        "hash": "sha256-enhoZ+DAu6QQ6p3ESkUzn0J2dKy+NVq7XiRmcxNU3WU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Azure.Core.Amqp.wasm",
        "name": "Azure.Core.Amqp.1f433ajq5m.wasm",
        "hash": "sha256-48soa5viLNrT2ijs+7ltYP5FjQieVn2SbCS5FP7BFfo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Azure.Identity.wasm",
        "name": "Azure.Identity.l7o4oy94x7.wasm",
        "hash": "sha256-kcOh26xDc+Be0J/sHoAZ0f5HYw2MFr35ufHnMBcJ+TA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Azure.Messaging.ServiceBus.wasm",
        "name": "Azure.Messaging.ServiceBus.3tn41kkwm9.wasm",
        "hash": "sha256-ef0+dDuZezDs3Kzusaj9QpdsukTY6XcicAQCtIwAKqw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Blazor.LocalStorage.WebAssembly.wasm",
        "name": "Blazor.LocalStorage.WebAssembly.5i6rpzuob8.wasm",
        "hash": "sha256-rV6t2aOA/VYCNE+WogAZB9ivw+8BoGGG/y7tBxMHSas=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Blazor.Serialization.wasm",
        "name": "Blazor.Serialization.9mh7mjm2ky.wasm",
        "hash": "sha256-8TZ3VQyxtcJW4Tkq9hwUu+eDRQTXKznDOA0YvocvqFk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "BlazorMonaco.wasm",
        "name": "BlazorMonaco.49jr5xdp2s.wasm",
        "hash": "sha256-yAwz/zW+JyPdpSD3J0Nn/uk6efxr4ztcbQovXbqulKM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "DistributedLock.Core.wasm",
        "name": "DistributedLock.Core.lg6tmiyjs8.wasm",
        "hash": "sha256-9QPTeXLYMOcmk7Y2OrnrlFHvLziFM2i1RU+7OGh36qk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "DistributedLock.Postgres.wasm",
        "name": "DistributedLock.Postgres.bv984j1eby.wasm",
        "hash": "sha256-9yNQUSBu2PQTMs1hDXKnrMwL/q4eUCGNORMpZUTw26o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "FastExpressionCompiler.wasm",
        "name": "FastExpressionCompiler.7so430m4ap.wasm",
        "hash": "sha256-d5myXreAc8cmWX9T9a8HD92NPgsc2i6sMIDnn4E/bzk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "FlakeId.wasm",
        "name": "FlakeId.sz4n67dq3k.wasm",
        "hash": "sha256-+PQhPbMi7mXyxox6mmL+Z3wFWBWPE9xkgoRVHaD9OUI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "FluentValidation.wasm",
        "name": "FluentValidation.yuptatg8bv.wasm",
        "hash": "sha256-1vkL1fNCyvLkWYtavCvKdUZS0BPY392ec7LutoPTps4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Google.Protobuf.wasm",
        "name": "Google.Protobuf.uhk89m0lh7.wasm",
        "hash": "sha256-whr/PTlhZeo66aP4hvRF71vQikzjIj3T/AHYnhNhfkw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "HtmlSanitizer.wasm",
        "name": "HtmlSanitizer.nkc90zzwkj.wasm",
        "hash": "sha256-GbxNXXnF23iB3vUqJtKENGudNXAXNET0d67a4hY6wAE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Humanizer.wasm",
        "name": "Humanizer.oqup3v7t3k.wasm",
        "hash": "sha256-4NbSboZzzP9nikRtXapUZNzOyITt7ht9TNqCIQHr5OE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "JasperFx.wasm",
        "name": "JasperFx.li0oni0key.wasm",
        "hash": "sha256-Y+hOudHPBDWAICNR607YLojv5YinI2YotzHTmRireAM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "JasperFx.Events.wasm",
        "name": "JasperFx.Events.ak05m9xftc.wasm",
        "hash": "sha256-GvwaqlzmszctETKP22oLPzfZzPaZmvCY2gIUOVuphtI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.wasm",
        "name": "LlmTornado.d117zywg7c.wasm",
        "hash": "sha256-/zPjsi0hn23IsvQL0G2Ncnv/AGx85SXoerXlz3WcXGk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.Agents.wasm",
        "name": "LlmTornado.Agents.7ipynbd7i6.wasm",
        "hash": "sha256-n8hc/jlWcWlInNkLfky1lkltT57OLEGeLkLPPQrwK6M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.Contrib.wasm",
        "name": "LlmTornado.Contrib.g6hhnrvw4l.wasm",
        "hash": "sha256-yNsjZVvirmPZMlZNBSOOyydZkl8nqEoD1biIQDvuSuw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.Mcp.wasm",
        "name": "LlmTornado.Mcp.0kfyp3vx0i.wasm",
        "hash": "sha256-QFj6PKEFj6VjvRjzMnTntzc4Qs3p8C4nkpBapixffGE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.Microsoft.Extensions.AI.wasm",
        "name": "LlmTornado.Microsoft.Extensions.AI.5b56hlnrwg.wasm",
        "hash": "sha256-YbVyQRcIbY5bNOElwpzh7yHAlEYI8u1YkZoSu3J9v8c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.Toolkit.wasm",
        "name": "LlmTornado.Toolkit.dunhb35xpl.wasm",
        "hash": "sha256-9fkUAWDGT+8ICvrEPBtlVynri8dApoR38S3PDel/kJ4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.VectorDatabases.wasm",
        "name": "LlmTornado.VectorDatabases.fe4o8ijk2v.wasm",
        "hash": "sha256-R+xW6xE4LigXM7UX9UIritX1Acpz/860JmTMCpjlmuA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LlmTornado.VectorDatabases.PgVector.wasm",
        "name": "LlmTornado.VectorDatabases.PgVector.ifj72ga17e.wasm",
        "hash": "sha256-4j8ZRkwnoubxTJZburafhO4QMXEvxjijsHPa9RlH19o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Mapster.wasm",
        "name": "Mapster.kmbqcefth2.wasm",
        "hash": "sha256-XHGDariHNBhffp3b4FuIy5J3urANJ0W7PF+zWdBzQ2o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Mapster.Core.wasm",
        "name": "Mapster.Core.x32j8aqh8m.wasm",
        "hash": "sha256-beFOq05+3vQ6ulYEkiVS/iw7eDlbQyhnaD94fjNDRio=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Markdig.wasm",
        "name": "Markdig.yudascw6lc.wasm",
        "hash": "sha256-Vs+O4z19GqKw3tbW2sPDdGS0+/V6Y7ZqqYniikjBh58=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Marten.wasm",
        "name": "Marten.y5fw84qmxu.wasm",
        "hash": "sha256-xswYf4pH+bAXVhaHDLVrzu1fJDNnCDtk6Qn2mJEW3n4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "McfCs.wasm",
        "name": "McfCs.m3eiggb1b0.wasm",
        "hash": "sha256-4ldQoBY7HX1i3E1wf+tLS/S4aaNHVM+wLKK67fB9tpw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Agents.AI.wasm",
        "name": "Microsoft.Agents.AI.t5sndrdw29.wasm",
        "hash": "sha256-r8hwWfjawqOalIFV1Wq4j+Xd4RcmQ+uBitpprzSO4BI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Agents.AI.Abstractions.wasm",
        "name": "Microsoft.Agents.AI.Abstractions.4j7stv6jnn.wasm",
        "hash": "sha256-4LG++pOd7H/ffe35dXEHxYe6NUbBqJGXaUdcg44rB+8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Authorization.wasm",
        "name": "Microsoft.AspNetCore.Authorization.wp5b4xwrtz.wasm",
        "hash": "sha256-TKbWsc5gCE+04slSkrqeNGTb7K8LiRW72Tm+1gzKxqU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.wasm",
        "name": "Microsoft.AspNetCore.Components.4payhwuyuh.wasm",
        "hash": "sha256-qPvavUxiWPAj51uJSf4LOekNKr4akGxRwNr0tHTe7iU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.Authorization.wasm",
        "name": "Microsoft.AspNetCore.Components.Authorization.qjl31ej8ln.wasm",
        "hash": "sha256-KqdtmmruNkTzucKeaRIgrg1AxAX16+kkoNMTM6Ov8tY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.Forms.wasm",
        "name": "Microsoft.AspNetCore.Components.Forms.ec8b0m8vzy.wasm",
        "hash": "sha256-AqlQLnd2AUu1G5e+NO3E6tGfGEty0Vbudprc7K4DUhI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.Web.wasm",
        "name": "Microsoft.AspNetCore.Components.Web.j6d7zpk03e.wasm",
        "hash": "sha256-e4X4HowGyVJz7RZ2UWcSe9mssqm1IFxF6A1+4hM6+bk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.WebAssembly.wasm",
        "name": "Microsoft.AspNetCore.Components.WebAssembly.jt9rzx6tc4.wasm",
        "hash": "sha256-ic64++0UYs3SvlBLdn4+qwNWqwSG407oUE8u0eu16ak=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Connections.Abstractions.wasm",
        "name": "Microsoft.AspNetCore.Connections.Abstractions.9clppqavtv.wasm",
        "hash": "sha256-SfnwCQTZ51UmLAZrsv6kWU3PsBXJat8FRHNn/VGC0RU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Cryptography.Internal.wasm",
        "name": "Microsoft.AspNetCore.Cryptography.Internal.8x24xtqvif.wasm",
        "hash": "sha256-zJwk24I2X7s6makQFY44e3HGTnInfBGXyx4Qa7MoKL0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Cryptography.KeyDerivation.wasm",
        "name": "Microsoft.AspNetCore.Cryptography.KeyDerivation.227px7djka.wasm",
        "hash": "sha256-BkP3G5vTmhDBjsJhEBWxi6a6VaQU0FLe8Rs0k6EeKXY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Html.Abstractions.wasm",
        "name": "Microsoft.AspNetCore.Html.Abstractions.s0ubs17vfc.wasm",
        "hash": "sha256-vNyCaywGyqByr17kd/M5PSxUI49Rh28mdFVxQZiknh4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Http.Abstractions.wasm",
        "name": "Microsoft.AspNetCore.Http.Abstractions.iek203tevq.wasm",
        "hash": "sha256-yg81LpbZyuwcyyrkZTM3aTKMChytWdVOICSE9Ci0KW4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Http.Features.wasm",
        "name": "Microsoft.AspNetCore.Http.Features.pqxjsc5k5q.wasm",
        "hash": "sha256-bzIPAVp+XhD63CRn9cVTB4tJRU+oDEAuwFYoHytMhUk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Metadata.wasm",
        "name": "Microsoft.AspNetCore.Metadata.bhaqktdl3q.wasm",
        "hash": "sha256-j++RGo33BhgVa0g5y83+TSz6cs3qmH+A5uEXVKZhtr8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Azure.Amqp.wasm",
        "name": "Microsoft.Azure.Amqp.zfgi5o1z23.wasm",
        "hash": "sha256-v3j8W7UhRZViqucyI31P33BvrulNouvC8tLzuLDwPSo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Bcl.AsyncInterfaces.wasm",
        "name": "Microsoft.Bcl.AsyncInterfaces.cejwwy890b.wasm",
        "hash": "sha256-NNIpbE3kgpNajuEB8IiMmzURems5JRUSriD/qNIPrqY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Bcl.Cryptography.wasm",
        "name": "Microsoft.Bcl.Cryptography.eoo0iygfe9.wasm",
        "hash": "sha256-7lYAc+yjYuc7rEBTFd08iTFrHY/keh+HT64gveqNCdo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Bcl.TimeProvider.wasm",
        "name": "Microsoft.Bcl.TimeProvider.gvcayy43x3.wasm",
        "hash": "sha256-VHbDm7kOBWbDcpjV8FEUOqhXx6MJis8JTcxWWae/Mrk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.wasm",
        "name": "Microsoft.CodeAnalysis.erp1n29u9n.wasm",
        "hash": "sha256-VvgzZZPZ5B7lUxBoceWqgqrawKs/BElsjRu+YB9hkIk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.Workspaces.wasm",
        "name": "Microsoft.CodeAnalysis.Workspaces.c3dihulv86.wasm",
        "hash": "sha256-yAu2rqqQvhof4W5kbWNInCtLmYIYPbm/0vHEJfVe6h8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Data.SqlClient.wasm",
        "name": "Microsoft.Data.SqlClient.ju7ib8r6fs.wasm",
        "hash": "sha256-OzNH/IqGkNt/f/RSopfrU8B0vc7tmCcHoOzwlM/0e1g=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.AI.wasm",
        "name": "Microsoft.Extensions.AI.pisiope01q.wasm",
        "hash": "sha256-H6PluIisFiGk5q1UzBmMAasHMsoZlqz/UNPcjhX1PT8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.AI.Abstractions.wasm",
        "name": "Microsoft.Extensions.AI.Abstractions.7u8zn6qppj.wasm",
        "hash": "sha256-gbmIgbmtS7iaqoawknvrCJy+IacuMGFbphC0lzFWNCo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.AI.Evaluation.wasm",
        "name": "Microsoft.Extensions.AI.Evaluation.oq6ehuogi4.wasm",
        "hash": "sha256-oII8ckiwRURjSPltf2wzbZFZqqwSDMVWdBzM8DINNJo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.AmbientMetadata.Application.wasm",
        "name": "Microsoft.Extensions.AmbientMetadata.Application.5qhbpwc6km.wasm",
        "hash": "sha256-g1hcjTdeGlfjOthgAkircnFK+msFvJNmB303z5IWPjk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Caching.Abstractions.wasm",
        "name": "Microsoft.Extensions.Caching.Abstractions.4odr42mpnw.wasm",
        "hash": "sha256-DBbq/+rBdNfwW6tNFfzzkjuD5OoB9Geryzpdsgxxi5o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Caching.Memory.wasm",
        "name": "Microsoft.Extensions.Caching.Memory.6z3ajjz4f1.wasm",
        "hash": "sha256-Xn7sOZZjP5fk879A/w1ejyt8tzsXqeBhqefdPMsnzpM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Compliance.Abstractions.wasm",
        "name": "Microsoft.Extensions.Compliance.Abstractions.n21mfdufbg.wasm",
        "hash": "sha256-Ee7gPXMqi9GRB0aKsrWlBmU4Mya7Zk/p73Y6yZrxyUI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Compliance.Redaction.wasm",
        "name": "Microsoft.Extensions.Compliance.Redaction.icdi0s85lh.wasm",
        "hash": "sha256-7jvQcxxv+xikYS6DAChWFiu7jzb4hvt4xD9XmuW6N6g=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.wasm",
        "name": "Microsoft.Extensions.Configuration.s6epnzhbd8.wasm",
        "hash": "sha256-7DgCCV/LB2eNVzPeNmZ5m2thp6g2kVsit8sogYccXwQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.Abstractions.wasm",
        "name": "Microsoft.Extensions.Configuration.Abstractions.wa2ojg94fi.wasm",
        "hash": "sha256-qOTZIeG9FsN1IhAycfVSHdCrxcSAjUo5paRUEfUJYhI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.Binder.wasm",
        "name": "Microsoft.Extensions.Configuration.Binder.l7o5knh4f1.wasm",
        "hash": "sha256-L5Yax0NPgj+DBfLt7lTLrH7RZVUZ/GeLukF9oyqJ7pU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.CommandLine.wasm",
        "name": "Microsoft.Extensions.Configuration.CommandLine.wxyk6n3acu.wasm",
        "hash": "sha256-5B9NNWuMjNJLEytaIHMtJeYHKgGOj/ZuEya89pjMQ8M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.EnvironmentVariables.wasm",
        "name": "Microsoft.Extensions.Configuration.EnvironmentVariables.ar5n9w2cmy.wasm",
        "hash": "sha256-5sXSl8t0AD0wPd1KIRrzIaMY/gl7ym2aCZyZGfDKSzY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.FileExtensions.wasm",
        "name": "Microsoft.Extensions.Configuration.FileExtensions.ke1t9ns7xl.wasm",
        "hash": "sha256-XJGOiagKd+9+kp4Ty7ffOtSndz0apzO8w53DJnndXmI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.Json.wasm",
        "name": "Microsoft.Extensions.Configuration.Json.k1hilos7yb.wasm",
        "hash": "sha256-OMRxiJp+p0LFgTIPl3n6LciGXvfYIdsr/Q88sETEF4w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.UserSecrets.wasm",
        "name": "Microsoft.Extensions.Configuration.UserSecrets.ij1ymsmt60.wasm",
        "hash": "sha256-TqJkF6naId5+NN/WrU4HBd7Y6yeDgKQgvOi3gdcZnb0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyInjection.wasm",
        "name": "Microsoft.Extensions.DependencyInjection.nun950pku8.wasm",
        "hash": "sha256-AWrVpqzFnmO0tCoEXAH0O7cE33X0Q6ZAa8lm5FRsMQs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyInjection.Abstractions.wasm",
        "name": "Microsoft.Extensions.DependencyInjection.Abstractions.q6idsb19uh.wasm",
        "hash": "sha256-rmpyVV0DOgiihi5oxsjrJb01S8m67/Z7tnu/auVgXRw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyInjection.AutoActivation.wasm",
        "name": "Microsoft.Extensions.DependencyInjection.AutoActivation.73d9bosbyj.wasm",
        "hash": "sha256-fYovJzpVyW31kNY6BzCtPL0zW9/Z2a6x4WN94lHxGHo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyModel.wasm",
        "name": "Microsoft.Extensions.DependencyModel.s9smm9iso8.wasm",
        "hash": "sha256-oPKJ9YzBRgsAz8UR7vFzqtVGdwra1LDKlsl2dE/wjsU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Diagnostics.wasm",
        "name": "Microsoft.Extensions.Diagnostics.f7zn8s1thp.wasm",
        "hash": "sha256-BaaWcXtZnSfywb3RXJuUpPEO8cLvZvlVHj3RJ10hPi0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Diagnostics.Abstractions.wasm",
        "name": "Microsoft.Extensions.Diagnostics.Abstractions.s62vewhs76.wasm",
        "hash": "sha256-1HqPfTJfvXForO2lQylRMNXcKDMdF9iD4VZSdaAX5qk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Diagnostics.ExceptionSummarization.wasm",
        "name": "Microsoft.Extensions.Diagnostics.ExceptionSummarization.w4atwrdort.wasm",
        "hash": "sha256-kBRhB7AcbjxKdFSALikf+0eigwmfG4zhy3z6173S4eI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions.wasm",
        "name": "Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions.3a6wlzletf.wasm",
        "hash": "sha256-Naed1Bo7/gOZyQefnxw9jI60ycjFYNAqRc5u2tIh0kY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Features.wasm",
        "name": "Microsoft.Extensions.Features.ueo71s0w5e.wasm",
        "hash": "sha256-Vgj+mE15z69ybirmlil+Ll2M8AaBfieIOIjKZQzZdx4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.FileProviders.Abstractions.wasm",
        "name": "Microsoft.Extensions.FileProviders.Abstractions.woov9lkdcy.wasm",
        "hash": "sha256-QNT4BJu7GuqNeD1vP+7WrlS/DU4fGK5J5eUsqmFJ4us=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.FileProviders.Physical.wasm",
        "name": "Microsoft.Extensions.FileProviders.Physical.4uvrzrkcx9.wasm",
        "hash": "sha256-7GjBXcHfSzByJQUNWXPp0f32BDtR3+Ti4+rkizb4Ljo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.FileSystemGlobbing.wasm",
        "name": "Microsoft.Extensions.FileSystemGlobbing.jnewm5d55t.wasm",
        "hash": "sha256-LyxUtk8WTHguCUKCEwF2EMVVVVGlw07/472TEvDsVfE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Hosting.wasm",
        "name": "Microsoft.Extensions.Hosting.squp8gogis.wasm",
        "hash": "sha256-HNA2eA4aAZOPG6C/DOQio1621LXtU3yi5Wc8S1P641o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Hosting.Abstractions.wasm",
        "name": "Microsoft.Extensions.Hosting.Abstractions.4h6ru2j5ry.wasm",
        "hash": "sha256-iO+hjWmByUkHZbi+88mu04rk1+nEGTnxEwcA5V9oiQ4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Http.wasm",
        "name": "Microsoft.Extensions.Http.0emfrzn4z9.wasm",
        "hash": "sha256-GPbtQluRylhBY8EIGiAoLfS27+XgYA9D7WkXi0FfWJg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Http.Diagnostics.wasm",
        "name": "Microsoft.Extensions.Http.Diagnostics.itoyx7yufn.wasm",
        "hash": "sha256-2nDIeIxPijqy/BmNpfSGWJ6xjTyKUnoSMQflNrevGeA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Http.Resilience.wasm",
        "name": "Microsoft.Extensions.Http.Resilience.1q8e7ur3ev.wasm",
        "hash": "sha256-V833xfew0gatgXW8jmT8caVbNP0akRF9HLkFMVbIUKI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Identity.Core.wasm",
        "name": "Microsoft.Extensions.Identity.Core.qm3gnrlksz.wasm",
        "hash": "sha256-Yok5IMMlCOh2uLDtrsdfX0NZqGf515ZnSQfSBR+rrs4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Identity.Stores.wasm",
        "name": "Microsoft.Extensions.Identity.Stores.ulznlw675s.wasm",
        "hash": "sha256-AunSaPjyRrVaR9FBD1yt3M9uHGO0XUshFqAQ4u7NgkU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Localization.wasm",
        "name": "Microsoft.Extensions.Localization.u7c9autpa7.wasm",
        "hash": "sha256-GjjVFRcxwwgJCCe2eila2/slVEmcse8hZwTEGygAEwM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Localization.Abstractions.wasm",
        "name": "Microsoft.Extensions.Localization.Abstractions.fiukxich8r.wasm",
        "hash": "sha256-dyBnJaTnKbduC2RvcfVH53qh98RF6W9s6gjasabzWvA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.wasm",
        "name": "Microsoft.Extensions.Logging.xispqyz68y.wasm",
        "hash": "sha256-V9FEOzDldETmKyiW3FTnoHpCZ4AZvoexnPaEIr09WZE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.Abstractions.wasm",
        "name": "Microsoft.Extensions.Logging.Abstractions.62zefycurg.wasm",
        "hash": "sha256-/h+gE4OLQdh3yuKWe0r8/2Si4zxcgw/fbOrbjMlX/ys=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.Configuration.wasm",
        "name": "Microsoft.Extensions.Logging.Configuration.epgs1g6cgz.wasm",
        "hash": "sha256-bnsm8cPjjbaSyl4ybIQn4voYn1srobBZ6VplvyvbPwo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.Console.wasm",
        "name": "Microsoft.Extensions.Logging.Console.oe0rhxn1x4.wasm",
        "hash": "sha256-MEBAhVkGO3yGe2bA39bB3rC71fI9Br6O2ktAKYaFLVs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.Debug.wasm",
        "name": "Microsoft.Extensions.Logging.Debug.npbd1pfl4k.wasm",
        "hash": "sha256-da4RJqKrmTG9d4/25G9sGoBvEuErKgGw36pwMgEIMd4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.EventLog.wasm",
        "name": "Microsoft.Extensions.Logging.EventLog.0bj6cn30gq.wasm",
        "hash": "sha256-PB8yHQp8NDlqDTSQC5xaAXc5o3PzdO97q7j8tyx01xY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.EventSource.wasm",
        "name": "Microsoft.Extensions.Logging.EventSource.ymyrlsfdz9.wasm",
        "hash": "sha256-Gj1qIgtSCYoCLHFqIpTDUvjqy7NTCJH8RmUhDO4v0L4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.ObjectPool.wasm",
        "name": "Microsoft.Extensions.ObjectPool.enzc200100.wasm",
        "hash": "sha256-hj7lDPMKixnbb0rCk4xdw2PO1sl0Z1ZaG1+N581GO3s=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Options.wasm",
        "name": "Microsoft.Extensions.Options.bs7sofzszu.wasm",
        "hash": "sha256-a/pTUQjNwOxhBz/lZgqVyyTyXrbdvyuKnFNyaVNH0Z0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Options.ConfigurationExtensions.wasm",
        "name": "Microsoft.Extensions.Options.ConfigurationExtensions.i2sp1sqcbf.wasm",
        "hash": "sha256-pnQwodkGYFmAdSWA6bpOdTxxgWZdqCkOlv0Ku086DvE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Primitives.wasm",
        "name": "Microsoft.Extensions.Primitives.ub9q6nskn2.wasm",
        "hash": "sha256-RgwNm087C7Ho0ns9NEvi/NHblJKJLGtNQvt9F9cpNrc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Resilience.wasm",
        "name": "Microsoft.Extensions.Resilience.cp4mwjgt2n.wasm",
        "hash": "sha256-UE0aVLtFkEQYGlcpDKcZ3C5faKj4T6jOG4lzonBObAA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Telemetry.wasm",
        "name": "Microsoft.Extensions.Telemetry.j6ale0nocc.wasm",
        "hash": "sha256-NZsN7yPP5cOMQJmuod0vxSV/TqYOPAd5ftw6IHsz7Ss=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Telemetry.Abstractions.wasm",
        "name": "Microsoft.Extensions.Telemetry.Abstractions.xucdubmidj.wasm",
        "hash": "sha256-WUx0QderqHcQNwtqae6fAMmAUrrUP2zN0h46ZR0YXow=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Validation.wasm",
        "name": "Microsoft.Extensions.Validation.y25xlv978u.wasm",
        "hash": "sha256-+vj2jK2O52sn5ULpvYCNDVIjlE7JxEgdA5PDj/ukV6k=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.VectorData.Abstractions.wasm",
        "name": "Microsoft.Extensions.VectorData.Abstractions.f85tsw18cr.wasm",
        "hash": "sha256-QRRgstei4MKqg1p05m0Wm9Kjdkeo3CaQXHvnMI3UMzU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Identity.Client.wasm",
        "name": "Microsoft.Identity.Client.12f530gc5h.wasm",
        "hash": "sha256-eb7lwCEBA/RlbIP/SnXkNUzDBJ518VXerB2BwC7/+uo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Identity.Client.Extensions.Msal.wasm",
        "name": "Microsoft.Identity.Client.Extensions.Msal.319owp3lr6.wasm",
        "hash": "sha256-SSiFPmQ8DN+ZNICu53YgYqQzZO4O/G05Y718Vz/zYdg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.IdentityModel.Abstractions.wasm",
        "name": "Microsoft.IdentityModel.Abstractions.bnwc1c8iol.wasm",
        "hash": "sha256-uX7I9eEF6dL6UdJ34ZRv/uvGbLoJ/bfiy9AnYCvT+oY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.IdentityModel.JsonWebTokens.wasm",
        "name": "Microsoft.IdentityModel.JsonWebTokens.cxyxl1fpw4.wasm",
        "hash": "sha256-EDjT0oUctyIFx92oz/BslCa3kmLMKgUoNbKvEHaadg8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.IdentityModel.Logging.wasm",
        "name": "Microsoft.IdentityModel.Logging.a1qebn9qtt.wasm",
        "hash": "sha256-ss2LB3619euRtb7la7zoT6dDsNtoRk0DfaRrHzMfW/U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.IdentityModel.Protocols.wasm",
        "name": "Microsoft.IdentityModel.Protocols.homtf5mctj.wasm",
        "hash": "sha256-ERvqChyVYiFjNDbPkS0sOag9BFOOIe8nWTEiBVLRbeA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.IdentityModel.Protocols.OpenIdConnect.wasm",
        "name": "Microsoft.IdentityModel.Protocols.OpenIdConnect.45hz3ik893.wasm",
        "hash": "sha256-iOxZiIbSNsoZ8tto2rBtzh77EzkRa0YcPb36KYr6IdE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.IdentityModel.Tokens.wasm",
        "name": "Microsoft.IdentityModel.Tokens.2wpk0uxouu.wasm",
        "hash": "sha256-7vcEr6jIbsLxtJsjKqRaUjlXzJK74GyMDqcqitijPxY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.IO.RecyclableMemoryStream.wasm",
        "name": "Microsoft.IO.RecyclableMemoryStream.k69j9tcsp2.wasm",
        "hash": "sha256-PLLNYyORp9p97V0x5KgrbBal9u/7enJGj68o7bI3pgU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.JSInterop.wasm",
        "name": "Microsoft.JSInterop.zhfbg74p0e.wasm",
        "hash": "sha256-ydDa5CE8UQzUry68CwEPAVbu361httdsDXT2AQmuMK0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.JSInterop.WebAssembly.wasm",
        "name": "Microsoft.JSInterop.WebAssembly.vi1uw8swyy.wasm",
        "hash": "sha256-yZy5My09/M7owHVzBcnK3BPDrUtQH3/wmFTaiPscAgo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.ML.Tokenizers.wasm",
        "name": "Microsoft.ML.Tokenizers.n79l5lcst3.wasm",
        "hash": "sha256-HYUkoQU+3cbJZxIMjcOsDT9VhvVmoZ5lQPxHfymn6sI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Orleans.Core.wasm",
        "name": "Orleans.Core.rb6vfxrnc0.wasm",
        "hash": "sha256-xzTwMdkZDhAozZyDwoE/aW2iCqaSyulrtCHNxPcWVd0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Orleans.Core.Abstractions.wasm",
        "name": "Orleans.Core.Abstractions.i8i4hwudh1.wasm",
        "hash": "sha256-rnkyongwX8jhCVFDSUyX7WxMmDWicpa/zJ7DavIiEs0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Orleans.Serialization.wasm",
        "name": "Orleans.Serialization.xtzwg81b37.wasm",
        "hash": "sha256-ZX0hYxgADGdYu4/z7GTdg6E5Ez5Cv5ZWHv5oXslaLRU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Orleans.Serialization.Abstractions.wasm",
        "name": "Orleans.Serialization.Abstractions.1qpko9gcmv.wasm",
        "hash": "sha256-x2T8DrM0IGaiL83AuynBWkKe4mFG7RDMSU9HO0774TY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.SqlServer.Server.wasm",
        "name": "Microsoft.SqlServer.Server.yamodpu5qp.wasm",
        "hash": "sha256-Fig+5hq00gGQlXAgSnyFlUlWyhlx9f+yPJb4INt3gNc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "ModelContextProtocol.wasm",
        "name": "ModelContextProtocol.s1j2jey7xi.wasm",
        "hash": "sha256-EGiFPCkostyrgW+AE5LWBBJsNJhWcJxo5friE7gNBCQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "ModelContextProtocol.Core.wasm",
        "name": "ModelContextProtocol.Core.wtc6tfwmee.wasm",
        "hash": "sha256-dN8c56JnA6IYtrnAw33+nTak9texmp5QskzonDeC3ow=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.wasm",
        "name": "NAudio.ijyss599m5.wasm",
        "hash": "sha256-3j15rJBDgE4SS0QPTvZ+/4ZIT7lnCS92t4An/4+a/cI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.Asio.wasm",
        "name": "NAudio.Asio.00rfw9qkdu.wasm",
        "hash": "sha256-cLq4+4itLXgdGHwbpxRkkFknjpAepyex7r+WFhrZvy0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.Core.wasm",
        "name": "NAudio.Core.j0335j43gw.wasm",
        "hash": "sha256-A4MDi1qeGex9OG+wMiBRRbv7kZPfpICDb+ZBy6FcRzQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "LameDLLWrap.wasm",
        "name": "LameDLLWrap.ub6mjianpa.wasm",
        "hash": "sha256-iqT1bT2L62pIM25wxLq6FVz1edK18tOGkstCZ7M+/wo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.Lame.wasm",
        "name": "NAudio.Lame.8lbf6uwjc1.wasm",
        "hash": "sha256-bKYkUZkQCwp+KxhQso3+1mnpSCqDm2Q6G0nbI4uoDJI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.Midi.wasm",
        "name": "NAudio.Midi.h35gybwxle.wasm",
        "hash": "sha256-zSu95Z/s6YcZf/x2prplNLePWLWv7GOxINK1IeXe/OA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.Wasapi.wasm",
        "name": "NAudio.Wasapi.luvxod8qds.wasm",
        "hash": "sha256-fYshxlURqo93wR4P9TUIonA8YLfXyKEvfqJ+5t61LEE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.WinMM.wasm",
        "name": "NAudio.WinMM.on5foakwha.wasm",
        "hash": "sha256-nHWlTd7j/qT4iMkM6W//Hv0qwpA+3XVEK7N5coduAwY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NeoUI.Blazor.wasm",
        "name": "NeoUI.Blazor.u86660k030.wasm",
        "hash": "sha256-4pSRwtBN4IfSI5PsbnfUVLj9uRVVFl+pm7BdXywZPnA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NeoUI.Blazor.Primitives.wasm",
        "name": "NeoUI.Blazor.Primitives.ymfjo6dku0.wasm",
        "hash": "sha256-6pUTz7qTY3MzvsPlFqWEjoPODgmEjEEdhsT2ZJkLIvI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NeoUI.Icons.Lucide.wasm",
        "name": "NeoUI.Icons.Lucide.l30lwnpycj.wasm",
        "hash": "sha256-PyDT3b3DlYO7sUwVlFzfOosBPtX1E+/S0x9TPz9NQ1Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NetTopologySuite.wasm",
        "name": "NetTopologySuite.j88r0fct5m.wasm",
        "hash": "sha256-jQGeO3n/jRGLzPfPtSCmZtK4AsF62ZmOPItYSbhLVHc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NetTopologySuite.IO.PostGis.wasm",
        "name": "NetTopologySuite.IO.PostGis.wd4mcg60g1.wasm",
        "hash": "sha256-tOtrlpqOrBsRDx4YTxVV7osiECBsbPp23Ecc3K1olJo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NewId.wasm",
        "name": "NewId.7inlox9ao6.wasm",
        "hash": "sha256-wUmUEY+7H2+6Y8JiVKqs8cotveoaoA83RrpVYO9l5zU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Newtonsoft.Json.wasm",
        "name": "Newtonsoft.Json.jcjjiqe038.wasm",
        "hash": "sha256-s8KVuknfxWl1cuDvQM/OnpBfnpM1rxzvzq21S1cF36U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Npgsql.wasm",
        "name": "Npgsql.t19fbx9wjg.wasm",
        "hash": "sha256-kXp2lNJ3M3ZFV+/Ofi0tO3RHT3N+ja3AEAZXQrx4orY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Npgsql.NetTopologySuite.wasm",
        "name": "Npgsql.NetTopologySuite.1qttmqjcf4.wasm",
        "hash": "sha256-RxZaZ/Iu5Q+y+5bz4dp1hX12f3cI8FA0UJ0ED38WPc4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "OpenTelemetry.wasm",
        "name": "OpenTelemetry.cgkmhno5gg.wasm",
        "hash": "sha256-DD65mpirnLNR77kP9AKT3a7kG4MvKYswqUYcNJAx+0U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "OpenTelemetry.Api.wasm",
        "name": "OpenTelemetry.Api.3isdtjnk58.wasm",
        "hash": "sha256-dVU4oPmSrSu40ghDHygDdx+RrkDVfutt1zrHRbtofMk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "OpenTelemetry.Api.ProviderBuilderExtensions.wasm",
        "name": "OpenTelemetry.Api.ProviderBuilderExtensions.u6pbi1uajf.wasm",
        "hash": "sha256-ulXe4NhQP9rDGjqgFrX9YuefTJF0XiNgEV8gvLMS58M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "OpenTelemetry.Extensions.Hosting.wasm",
        "name": "OpenTelemetry.Extensions.Hosting.zx4bx8sh93.wasm",
        "hash": "sha256-x86XjBp4EP0EKp9XkfmEbHaA94fpWvNILy1epRJWWvA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Polly.wasm",
        "name": "Polly.pos1ijmk3h.wasm",
        "hash": "sha256-eSXY+B0UR7o5vq1w0+MYQU84lXRsKRij1hP/2bsV86I=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Polly.Core.wasm",
        "name": "Polly.Core.len66yyb84.wasm",
        "hash": "sha256-/SldWOekwJBvtBoLrZGi0ChS8yXNpxmsqYGh9XAxSmk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Polly.Extensions.wasm",
        "name": "Polly.Extensions.9hic17cyez.wasm",
        "hash": "sha256-jUVw+9fLB9IonDYEW7GKrJuCJfimaJldzZ4XTHB04ro=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Polly.RateLimiting.wasm",
        "name": "Polly.RateLimiting.chhix0v2ww.wasm",
        "hash": "sha256-AKZljWiUs0PH7+40lFA0S+sVW6CM4J5zulgglZYLKY0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "RabbitMQ.Client.wasm",
        "name": "RabbitMQ.Client.m5501kilve.wasm",
        "hash": "sha256-GuuPs0Z4aMIcz+Ms5EiqzMRlWOUBcsa68iv9NcToabU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Radzen.Blazor.wasm",
        "name": "Radzen.Blazor.vyj0ltayl4.wasm",
        "hash": "sha256-6bFQI8JvJUuHdkv9Fto01Kktb/3OArumCH4BpMqW+O4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Refit.wasm",
        "name": "Refit.sogb4xk4un.wasm",
        "hash": "sha256-PBFuCYJUT1pPAlB9SNhwH7I4UpQHGkhYYxC7taY8KBQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "SecretSharingDotNet.wasm",
        "name": "SecretSharingDotNet.tw031eki97.wasm",
        "hash": "sha256-o39cLtz845PkqLFaB1P3FtnjDOJ57RzqTzg9EJYm3WQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.wasm",
        "name": "Serilog.djuoe46zwr.wasm",
        "hash": "sha256-mS78vNJt6Rhh8KhUHuwewRZPV09f/TB7HJ80FWeVVrU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Expressions.wasm",
        "name": "Serilog.Expressions.sbot8sqyka.wasm",
        "hash": "sha256-aDjkwQ/f5ZgKB3b5Q5KLyc6H3D5BGVNgxuDEwNt6PGo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Extensions.Hosting.wasm",
        "name": "Serilog.Extensions.Hosting.oqive7ltqe.wasm",
        "hash": "sha256-YLVcId9gJc7eZxKJN/X/Fky18bne6RRjAPXvCWy9yJI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Extensions.Logging.wasm",
        "name": "Serilog.Extensions.Logging.4aarm1ia2h.wasm",
        "hash": "sha256-NHk6xRS3dIv7nLWuBUZvce3EXz7kvxiBsrjE7KvSMcI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Settings.Configuration.wasm",
        "name": "Serilog.Settings.Configuration.19ux1r71j2.wasm",
        "hash": "sha256-m0tMCRO1iG7T5hwKE5+IBQ6ZBCN6GriLj1CXbqbIjwc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Sinks.Console.wasm",
        "name": "Serilog.Sinks.Console.jps0r0fyh5.wasm",
        "hash": "sha256-qQMQoLrp9l242W6yL7TUuGDDYZJ0LuJ1cyZrhIJPl0Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Sinks.File.wasm",
        "name": "Serilog.Sinks.File.2xjoy8x0ef.wasm",
        "hash": "sha256-KvMWoiK6uwOzjc9HiRXtCYQvPVG8xOQPK8OLvVqRMq4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Sinks.OpenObserve.wasm",
        "name": "Serilog.Sinks.OpenObserve.c7mkdw8sr1.wasm",
        "hash": "sha256-aNrW8KrmisU9D5upVW9YkdpFuDGEA1wrSO/Ol4HpVlo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Serilog.Sinks.PeriodicBatching.wasm",
        "name": "Serilog.Sinks.PeriodicBatching.9t16jns3jj.wasm",
        "hash": "sha256-U4IQBk7KskKmfT3CQXom7id19ruIsvj3poLTezEbVSM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Spectre.Console.wasm",
        "name": "Spectre.Console.ghcprfmd4v.wasm",
        "hash": "sha256-jL1Fnlc45WTWQIZ7NOxkqoTQyiwabFc2Cazh6SPI5Y0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Spectre.Console.Ansi.wasm",
        "name": "Spectre.Console.Ansi.gv7iqb0uqi.wasm",
        "hash": "sha256-XN5Zd0RT4Td0twqGYLfqyhyrY9hsBrL9OTVeSJfKvmo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ClientModel.wasm",
        "name": "System.ClientModel.n70kmb0sl3.wasm",
        "hash": "sha256-EyAnEsN56ycGQLViFnz0uTHB4i+1thCCZdx0sn0gQw0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.AttributedModel.wasm",
        "name": "System.Composition.AttributedModel.xjif02d2is.wasm",
        "hash": "sha256-U1z94xgywn6hkn/QrGir0aAnxKs7nfoDKlBhV19H2TM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.Convention.wasm",
        "name": "System.Composition.Convention.jf1zeek5hf.wasm",
        "hash": "sha256-AciZSrESYwC4KU2/d9UJqwTgDDmOQA5hpmg50sDqHac=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.Hosting.wasm",
        "name": "System.Composition.Hosting.e34d2g66uz.wasm",
        "hash": "sha256-+rdwoU5P89ph2WqlaceKN8T/2juughU2SDidAHby32w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.Runtime.wasm",
        "name": "System.Composition.Runtime.ccm13dk3fm.wasm",
        "hash": "sha256-XF6nJ73dlc5VZe2GKAWby/hyvuaCrzuPZe8nHg2pJ/Q=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.TypedParts.wasm",
        "name": "System.Composition.TypedParts.oz2ut0mn5s.wasm",
        "hash": "sha256-pMd0NDsJG6d31aN31kU1bpOuZDEG7RaYleALWz38k98=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Configuration.ConfigurationManager.wasm",
        "name": "System.Configuration.ConfigurationManager.mxet61z93e.wasm",
        "hash": "sha256-JjXsEJZkBJHgBD+gucM96QjDL4E8dbo1xSuIXGCkP4E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.EventLog.wasm",
        "name": "System.Diagnostics.EventLog.1tgz42l5yb.wasm",
        "hash": "sha256-K2S5WVSXDXeVc/B3C+dIBbnG+/dcvsMbphY88hcbj6c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IdentityModel.Tokens.Jwt.wasm",
        "name": "System.IdentityModel.Tokens.Jwt.28de5g3cun.wasm",
        "hash": "sha256-jjAptokOXSXKs5qsKrmqiqE1SuTQT3MMuEZf2+qUlgc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Hashing.wasm",
        "name": "System.IO.Hashing.xb8fkje29m.wasm",
        "hash": "sha256-Ewhq7P1aycXQeXpaNUZFsXKhJBR7Aplp/IiBYG9CO68=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Memory.Data.wasm",
        "name": "System.Memory.Data.daz259hfhu.wasm",
        "hash": "sha256-Jdjg60Kh8nk3Q0a1rGi6iz6QS2s2cxLadug2XVqyMJE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Numerics.Tensors.wasm",
        "name": "System.Numerics.Tensors.8oopr9vw0k.wasm",
        "hash": "sha256-2Ac/7j26uYutvLalRkLr4bZs/OmPHDbOOkfOm4Pqbis=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Pkcs.wasm",
        "name": "System.Security.Cryptography.Pkcs.co2absxvec.wasm",
        "hash": "sha256-DNfD9zLnKkV59RAeNl/KNmBjABmtoKDjr+J0LJ5PkMU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.ProtectedData.wasm",
        "name": "System.Security.Cryptography.ProtectedData.nqtaejcl5a.wasm",
        "hash": "sha256-D6+lW9J4ST0TwfvMKilenbvNn1FEweZsJlQ463g28GY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.RateLimiting.wasm",
        "name": "System.Threading.RateLimiting.0l1hebbunv.wasm",
        "hash": "sha256-vNMjLpEER73MeVxOGkuGOXeEsa/nFXxbjRIpVDuPuxA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Weasel.Core.wasm",
        "name": "Weasel.Core.281z24hcf2.wasm",
        "hash": "sha256-Di0njYhjYEz6nPRVbt0uiu0t2Tn2fWZ4U2a8Z0gmBRg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Weasel.Postgresql.wasm",
        "name": "Weasel.Postgresql.93eg1wklnm.wasm",
        "hash": "sha256-q7MezHWZWjz4unVWv3PH1T/CwS2BuHalQkL+NFpX0BE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Weasel.SqlServer.wasm",
        "name": "Weasel.SqlServer.mfgk7p85td.wasm",
        "hash": "sha256-MrtckedhpqMfoqzsaIARAehBJmnQD3wQTggzz/Z8qPc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Weasel.Storage.wasm",
        "name": "Weasel.Storage.74c338pejd.wasm",
        "hash": "sha256-F3Ob5S3HA7IX5ATzupkynGKDCgqWFoLskp1ggEtH+uU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.WindowsAzure.Storage.wasm",
        "name": "Microsoft.WindowsAzure.Storage.om1rz7lixe.wasm",
        "hash": "sha256-AJAE5hRcRWpkc3BQY//bt1XvUzX6fpaDiG3xlH8GITI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.wasm",
        "name": "Wolverine.jj8qrpxwfb.wasm",
        "hash": "sha256-QUPbOWEdb/u+LKXQOqgC3KJmruHtK1fnBzGVnGwMrvc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.AzureServiceBus.wasm",
        "name": "Wolverine.AzureServiceBus.6nxb095pf9.wasm",
        "hash": "sha256-EtPqp+7GBTIBeEBXGdjIiEDXKtVE4C+LuLcmIuGIGgI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.FluentValidation.wasm",
        "name": "Wolverine.FluentValidation.2qs5v9sceu.wasm",
        "hash": "sha256-qiNF7bl/cphByoOu1kRU6Lxlgv7QMVP5M0mRpb2frn8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.Marten.wasm",
        "name": "Wolverine.Marten.9zbilm0duu.wasm",
        "hash": "sha256-JZpgB8BlDE1THR1IhEGKvcZQ6AKWuZHAu/McX0+FzBs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.Newtonsoft.wasm",
        "name": "Wolverine.Newtonsoft.0w8gl6hczo.wasm",
        "hash": "sha256-AFL8yRCfAWrS1iXmWBCF4y+kTSR9irqv6QSYtevpEn8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.Postgresql.wasm",
        "name": "Wolverine.Postgresql.ne76ccndw9.wasm",
        "hash": "sha256-/1afItAPgaZCP1m38p2i5YhcjKIstCA829mYz8K2uc8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.RabbitMQ.wasm",
        "name": "Wolverine.RabbitMQ.nqjybfitht.wasm",
        "hash": "sha256-b3/THtFuQeANJ3WubAOAZ69r2j73mBSvxvF341uOI/w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.RDBMS.wasm",
        "name": "Wolverine.RDBMS.j1g2bpmb4w.wasm",
        "hash": "sha256-STOuzxnZzFrJjtck7i6OZsJZ7tk2J4c0OCwZMi7QRNY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Wolverine.SqlServer.wasm",
        "name": "Wolverine.SqlServer.r0r16aojny.wasm",
        "hash": "sha256-9VLzKvn9GnrOhT0tlCeRuJrCmzee6+1fH/+v4E94Ibw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CSharp.wasm",
        "name": "Microsoft.CSharp.y8cabdi7yj.wasm",
        "hash": "sha256-foTztPRrzoq1jElVsdRv+okt60TEvQeYphwV9t9NDKI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.VisualBasic.Core.wasm",
        "name": "Microsoft.VisualBasic.Core.rj6qwnj2np.wasm",
        "hash": "sha256-WdAu9w+/3N6zBiVnPDG93KylB3tMbEvzetW+JVs/phQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.VisualBasic.wasm",
        "name": "Microsoft.VisualBasic.m5z1bpmsgy.wasm",
        "hash": "sha256-xkh4dkn7gNe1WLAQe3cfm3CjdRVNcJvlpSSdHWynwrI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Win32.Primitives.wasm",
        "name": "Microsoft.Win32.Primitives.i6x74v9vcb.wasm",
        "hash": "sha256-ulM/aFba69M17/Tj1tuJ2PpvdQPnNbGtRVryXQ4hBpo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Win32.Registry.wasm",
        "name": "Microsoft.Win32.Registry.y4gq7fo2lp.wasm",
        "hash": "sha256-zigLwYnX2uS92hS70FRqXnZISw2C3v0KY6hKX+fXkA0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.AppContext.wasm",
        "name": "System.AppContext.2bkkwkya64.wasm",
        "hash": "sha256-Msxz+VaXeSzLcO+27REusA3i8rZyDPExRwGOEKzVK2M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Buffers.wasm",
        "name": "System.Buffers.28ngaukdh3.wasm",
        "hash": "sha256-ktsOwh8KMvMN3Q8A2Mb8Y71n8aEHhVV1xQZzvE2Npyk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Concurrent.wasm",
        "name": "System.Collections.Concurrent.uigxbozvg6.wasm",
        "hash": "sha256-5gzik54yVqs8LFsqbaXng6lrn3RhlMDfCC8jLq2VZHk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Immutable.wasm",
        "name": "System.Collections.Immutable.jity4go0mw.wasm",
        "hash": "sha256-6w1JgRvP2NIlEP//vr+XQFKoJRWi0F+a6pwJB59tq6U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.NonGeneric.wasm",
        "name": "System.Collections.NonGeneric.t4c1msu741.wasm",
        "hash": "sha256-rVmOtWPpzaYEkyK81lYUn46nwxbzLjFlSj27xuHuoUg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Specialized.wasm",
        "name": "System.Collections.Specialized.rjq2931kbi.wasm",
        "hash": "sha256-55c3YH9310sCKy6fGqY4Zllu/y8OvSsTYjHn6vgtSks=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.wasm",
        "name": "System.Collections.0wg2pdla5w.wasm",
        "hash": "sha256-AJ/sh3LfpzJ6U8IlpLtJlxr7CkJA4R3q8GfHYYLuP7Q=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.Annotations.wasm",
        "name": "System.ComponentModel.Annotations.x1bvmbqcbl.wasm",
        "hash": "sha256-tctQ8UYwJJ8shbwNq007DYmG7LPwqPz+S0ofwAfcbO8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.DataAnnotations.wasm",
        "name": "System.ComponentModel.DataAnnotations.0ugm2kn0ef.wasm",
        "hash": "sha256-uIBMcrYQTLgnbuFLK9AicbBy9f1zxigmcq4uOhvRZ3c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.EventBasedAsync.wasm",
        "name": "System.ComponentModel.EventBasedAsync.iqz7yfxk0z.wasm",
        "hash": "sha256-qr6WZ92/fTRJqDjbCzCtcyRISjqpy6THwhvvVptQPs8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.Primitives.wasm",
        "name": "System.ComponentModel.Primitives.66wyka365l.wasm",
        "hash": "sha256-XoOP7YWbPst7iwpA+IVsqnFj35UVSNLQX0qd95WX50A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.TypeConverter.wasm",
        "name": "System.ComponentModel.TypeConverter.le29ulsuyy.wasm",
        "hash": "sha256-bQZSZ/geEtNaQeXITxyPdOnz5maRkPb0OLmq5b8dBfk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.wasm",
        "name": "System.ComponentModel.wj8g6avob9.wasm",
        "hash": "sha256-TK2nsOyraXFIh5vQiUCbhr1pLr+FmtVFfbPV0bsZvA4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Configuration.wasm",
        "name": "System.Configuration.7xj6hflys1.wasm",
        "hash": "sha256-W8cY5EWSRm4MysMCu596PIjDOw7/PGHbGXOo9q0cV8M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Console.wasm",
        "name": "System.Console.bdauwtppto.wasm",
        "hash": "sha256-Aeyntn8KICi8KijwpeMGqeuLLrf5re9RB8xfbQD+fuk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Core.wasm",
        "name": "System.Core.63xjv55d3c.wasm",
        "hash": "sha256-QnFF7W0rRqgKv3dhgaDpxeIv0rGcJmcd2TC4pwMfIhs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.Common.wasm",
        "name": "System.Data.Common.or5475be6q.wasm",
        "hash": "sha256-zD75iwjfjKKb57d/N4AYDNHvJ6nlzcKqI86L1GCKqFI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.DataSetExtensions.wasm",
        "name": "System.Data.DataSetExtensions.aie27nz2fz.wasm",
        "hash": "sha256-DncPkqYn3WSSuOXGrXqqDORmp24KsFKLbpo+0kYEKrc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.wasm",
        "name": "System.Data.s403jyj13x.wasm",
        "hash": "sha256-bE6W63dBZbAllE7txwGlEzFzTfKrHlSzzWBPrCOaw2E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Contracts.wasm",
        "name": "System.Diagnostics.Contracts.awjvmobi6a.wasm",
        "hash": "sha256-etzdUlIlT6ITWKDVaYadkvzxVF6uXsOwpAGEfRUnJLo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Debug.wasm",
        "name": "System.Diagnostics.Debug.zd32j0e68b.wasm",
        "hash": "sha256-p9AUpWapiBUcVVNRFi/p5xCLlcbZQE51E45GvIyG8ao=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.DiagnosticSource.wasm",
        "name": "System.Diagnostics.DiagnosticSource.h063kza90u.wasm",
        "hash": "sha256-MSH2kpV4JRkQdze0/1G3U9hK1KybKDnKOvkaU7mYrG8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.FileVersionInfo.wasm",
        "name": "System.Diagnostics.FileVersionInfo.5faw37z8re.wasm",
        "hash": "sha256-wYtSW2PUGZABZMPyfxFKdw4E/wKwaHYfMzeSE+LtvXg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Process.wasm",
        "name": "System.Diagnostics.Process.85y49vqmlw.wasm",
        "hash": "sha256-JNVVDKXz5lryO6yeAVcogoktKW0wn9Gzk18oFvUvW7Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.StackTrace.wasm",
        "name": "System.Diagnostics.StackTrace.y2jslxj8se.wasm",
        "hash": "sha256-ZidOHreDMi/G8aTupFo/T2T/sI9Azq+qCgFlQTG+MXg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.TextWriterTraceListener.wasm",
        "name": "System.Diagnostics.TextWriterTraceListener.ubfqtow1rg.wasm",
        "hash": "sha256-Gq3UhJVGKCsKOSadrTJmeBoEBIB1aQJ72wi830/RcTM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Tools.wasm",
        "name": "System.Diagnostics.Tools.fxjm3tg3oa.wasm",
        "hash": "sha256-Y0UtthJI9vonI4z+/+qVeMuu9BvQseV3W5Kqiok/ih0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.TraceSource.wasm",
        "name": "System.Diagnostics.TraceSource.m2df4u53h3.wasm",
        "hash": "sha256-7ruz7b/4taN1hslEvPDNya7jlAq9S1zU7lmtSSakJ3c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Tracing.wasm",
        "name": "System.Diagnostics.Tracing.yykvalakbz.wasm",
        "hash": "sha256-6tlwP/rw9w1Z8STeEKk244ROhuLPG14Tc5Oj2Q5SoWo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Drawing.Primitives.wasm",
        "name": "System.Drawing.Primitives.6ajpvk5hee.wasm",
        "hash": "sha256-IvOqWcdITeDzamSnAm41HZ6ZZw5s+TdJRLabbbwPhHg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Drawing.wasm",
        "name": "System.Drawing.pwn69jd1sx.wasm",
        "hash": "sha256-iaPXgmZStjRI791gA9FgGZi75saGgt1ZYR8BLjPQVWU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Dynamic.Runtime.wasm",
        "name": "System.Dynamic.Runtime.dwxo8l4rdr.wasm",
        "hash": "sha256-Y8JLZ0Q+Hq+Ue1lIam/t2R4RHRYTP2hsKwtdLLCFzbY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Formats.Asn1.wasm",
        "name": "System.Formats.Asn1.7r6f4x6f4s.wasm",
        "hash": "sha256-DWjLEno2gKPUwP685ugb0pFCHtmWn4oy4sP6cPEZV/g=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Formats.Tar.wasm",
        "name": "System.Formats.Tar.9uzzbcwdi8.wasm",
        "hash": "sha256-z4QPxqj5k7y27vorrM+w5Lb6PKiqGEC/V4iygoxfIRo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.Calendars.wasm",
        "name": "System.Globalization.Calendars.h9ciwjgoxu.wasm",
        "hash": "sha256-lVq7a0doxTh2Vdzn7wf95Ec5i6lGujvXY2h51TKCwaQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.Extensions.wasm",
        "name": "System.Globalization.Extensions.7iv43bve8g.wasm",
        "hash": "sha256-f9c3SyP0tYtSkV1QBevO7XVhgOr7ElNa8MGXINgPyAQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.wasm",
        "name": "System.Globalization.xg8c7skko3.wasm",
        "hash": "sha256-H7Pb+7TbWWnbEng/b6DeLvfgzAUpnKGLFAiKUPT+ztA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.Brotli.wasm",
        "name": "System.IO.Compression.Brotli.cgycdoss65.wasm",
        "hash": "sha256-bIbkKjMJR7l1Eb5D4ccKG402uQETLvgNDy0Si0Y+6MA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.FileSystem.wasm",
        "name": "System.IO.Compression.FileSystem.g80du2epa5.wasm",
        "hash": "sha256-kHfLrKhxx+6ZSM7H39Cj/GOK1BL9i3h1xfZBfsBzD64=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.ZipFile.wasm",
        "name": "System.IO.Compression.ZipFile.hq70g0m5k0.wasm",
        "hash": "sha256-KdcAEnDQx2xDH9z2aM5SNHrnGKuCEyGSYso5pH5pr4I=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.wasm",
        "name": "System.IO.Compression.ntu9cg6tk2.wasm",
        "hash": "sha256-tY/H2xmqAFiTHnxU8/6WOVCG3oOvp3Q36T+Ri12gYZY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.AccessControl.wasm",
        "name": "System.IO.FileSystem.AccessControl.bcre3rjv67.wasm",
        "hash": "sha256-66BQoQMVYbtLrbMUkas4ERFlUxxAUDkcbDfgDdrF7KA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.DriveInfo.wasm",
        "name": "System.IO.FileSystem.DriveInfo.gc4k28tlqi.wasm",
        "hash": "sha256-ADtnUkdgbU+XmzYlZKS3sdn0JX5AbRpy9rVIiIrGguE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.Primitives.wasm",
        "name": "System.IO.FileSystem.Primitives.be43v34w5x.wasm",
        "hash": "sha256-A+TZDIDlLB1ptZp36gHd+RLUU4zM4+ZPOLxudzxPBAc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.Watcher.wasm",
        "name": "System.IO.FileSystem.Watcher.8qc3wsh77q.wasm",
        "hash": "sha256-0LzsIBUxLN+Zm/auCG7U7PuycOU4LbpvMbAr0Y0/BdI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.wasm",
        "name": "System.IO.FileSystem.h8uqkh82lb.wasm",
        "hash": "sha256-b2xR11S13yOtUTmfYqWUcj4clfG8CpqTAGOcl9BvYSc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.IsolatedStorage.wasm",
        "name": "System.IO.IsolatedStorage.gzsye4pxc9.wasm",
        "hash": "sha256-xApXuMRImNGmM7zijGSXr+yCBzzPE679wsp7tk1sglA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.MemoryMappedFiles.wasm",
        "name": "System.IO.MemoryMappedFiles.4lxw69q3da.wasm",
        "hash": "sha256-+KNHSvZ3txWouIGreidDWZCKCygCEk2aIswbmYp/HKg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipelines.wasm",
        "name": "System.IO.Pipelines.u8n6swt2ok.wasm",
        "hash": "sha256-LifQPBrvqxFYfjzJY6xu+jsKV85XzBNlkK/ZEecWLLc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipes.AccessControl.wasm",
        "name": "System.IO.Pipes.AccessControl.ddt7du9svb.wasm",
        "hash": "sha256-j3oMbcOE033ryvYZ1UpWgz2JDdzqfyuZoINDD7XY9pc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipes.wasm",
        "name": "System.IO.Pipes.tv4o1w1yo1.wasm",
        "hash": "sha256-RyDhQqgIoxLo5y+9F17kNsNkC74iI4n/Lg/T2NwEzyM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.UnmanagedMemoryStream.wasm",
        "name": "System.IO.UnmanagedMemoryStream.jb5bmgvv05.wasm",
        "hash": "sha256-UUr5CkM630/je64hE0usuhtwSOwZICJaY0IiBqJBvhc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.wasm",
        "name": "System.IO.g204ske44p.wasm",
        "hash": "sha256-qNqGDefXxtmI6Y2xlF+3wmVeYRdJ7n8L4gdXZEsy8m8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.AsyncEnumerable.wasm",
        "name": "System.Linq.AsyncEnumerable.w2ftt5zfx7.wasm",
        "hash": "sha256-aK38b6Cs8BCUvR7kA91s5SkdRfpB5cR1Fd7c2lrlSSs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Expressions.wasm",
        "name": "System.Linq.Expressions.rwll9v3f54.wasm",
        "hash": "sha256-K9cXMgZg9w0hnksRk8PpoKoaHkwjWOMfLAEQXKeHuM0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Parallel.wasm",
        "name": "System.Linq.Parallel.78wbzaw40x.wasm",
        "hash": "sha256-5+JiBPrI7kdLCZ7yqsvuIJPNBhR6LtkkXKV8zrBabVM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Queryable.wasm",
        "name": "System.Linq.Queryable.bk0gkg2iiv.wasm",
        "hash": "sha256-29qySGTwBU0h5CGKfGtTw+CY1qfR7D0DIo2sm+6MohI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.wasm",
        "name": "System.Linq.bmrkhne1ox.wasm",
        "hash": "sha256-01WUz7Yv5hQ268Pf7fl0rHnNi25Ck3iSZDLt5m1pe2Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Memory.wasm",
        "name": "System.Memory.6sz098jj9v.wasm",
        "hash": "sha256-Jm7+XsbP0Ll97vwpeYJdmR8pUDsLOBOjaEVGq+C9eEc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Http.Json.wasm",
        "name": "System.Net.Http.Json.56y669zj3s.wasm",
        "hash": "sha256-fWzfpAk6aZRzGuXScLDlmyI3EXkyM9CypUDdUa7L6Do=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Http.wasm",
        "name": "System.Net.Http.08oi8qoit4.wasm",
        "hash": "sha256-IPslUryM8dVMBqWDGmwJOqFw2NE70E0TKHBKxWXwzTE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.HttpListener.wasm",
        "name": "System.Net.HttpListener.qg0j6amwue.wasm",
        "hash": "sha256-5ujUQXWdkVb8ENZ5zXA4gQJakpvEbymx2RmDHE970Os=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Mail.wasm",
        "name": "System.Net.Mail.533n6ks0cq.wasm",
        "hash": "sha256-YcW4pvIlOA0n74N6n6fMMsmtQh2G2Rd8wUvc3ag7RGA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.NameResolution.wasm",
        "name": "System.Net.NameResolution.s5btvjcq6y.wasm",
        "hash": "sha256-QDfh/6RWl8mhbIZnLp0F6Ht518QlZNHfFNAGbACj4tY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.NetworkInformation.wasm",
        "name": "System.Net.NetworkInformation.ucbqjhtjil.wasm",
        "hash": "sha256-At2HflEs9TiHOLkqi0C9uHSUc6W6ggZl5mmPtQ70h98=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Ping.wasm",
        "name": "System.Net.Ping.ustem6o7fc.wasm",
        "hash": "sha256-Xnw6mzltKz5abYZcmWscptzw8YDn9G7oKkQqgzZ2L1c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Primitives.wasm",
        "name": "System.Net.Primitives.935qhm3yb8.wasm",
        "hash": "sha256-RRU91O3azj9Hpy1my1LrAgkWCSQnuJEETf3U1dARGC8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Quic.wasm",
        "name": "System.Net.Quic.cnkjqkyrlb.wasm",
        "hash": "sha256-SPl+qTwgQTcGqmoi3kHf2e1Vve6P7Q+rp/B8CNbtDJY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Requests.wasm",
        "name": "System.Net.Requests.7w1r8uf0a4.wasm",
        "hash": "sha256-qY2UjpCNzp3MWqBSN0rWK+NoIqsox2XfMVY6DaSetO8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Security.wasm",
        "name": "System.Net.Security.xlqynrdyot.wasm",
        "hash": "sha256-Nc4p2mSzW5BUO2Sk5pWpwE7DnOdBE8Laxh+N2ESNMt0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.ServerSentEvents.wasm",
        "name": "System.Net.ServerSentEvents.vwcoz3tazw.wasm",
        "hash": "sha256-oWmHBCyId3b5/cbJBZQeU/Y+xXqWotSiIvoMcxat0ZU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.ServicePoint.wasm",
        "name": "System.Net.ServicePoint.8k7uo97505.wasm",
        "hash": "sha256-OPKuJnS12kqgLWBaqqR2p4clW7f/Kz6XqzO1eXZRB14=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Sockets.wasm",
        "name": "System.Net.Sockets.krbyf8n4l7.wasm",
        "hash": "sha256-8JDtmlYFyYP3Bvv5f9JiGKjhqINet8gjwbERDmn4Exw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebClient.wasm",
        "name": "System.Net.WebClient.s7es5vcafa.wasm",
        "hash": "sha256-+He4X18r5PRKcxFeDHkC9+5aPZb5ciN8CK3kQcANit8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebHeaderCollection.wasm",
        "name": "System.Net.WebHeaderCollection.9quxghu2aq.wasm",
        "hash": "sha256-L5Im66UgOUCTv+gWwTLFnvaC2+14u/dfO0w3KFESBfc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebProxy.wasm",
        "name": "System.Net.WebProxy.ivbmxiwm57.wasm",
        "hash": "sha256-nq4JOOk8Mu0Tu8QO2O3gYrIHiYvYBx8hsSumxJK49+I=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebSockets.Client.wasm",
        "name": "System.Net.WebSockets.Client.x9i7zul9bw.wasm",
        "hash": "sha256-u9wSj7Qki++uQCOriQ5b5jnVPvd8AF6TYzo1cRQbBnI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebSockets.wasm",
        "name": "System.Net.WebSockets.ep3trdpcsb.wasm",
        "hash": "sha256-ApeOH9Go2vMvwnpus7yN0CbpFrmVqvrUzyNiqQ4nMa4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.wasm",
        "name": "System.Net.lkc10g164w.wasm",
        "hash": "sha256-5cVTGqwp/o594m8mPq30CCXdD5PCuHiSw00BaSNwMoY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Numerics.Vectors.wasm",
        "name": "System.Numerics.Vectors.ohk6rx7mq3.wasm",
        "hash": "sha256-xENvAVz3MoraJsO1Do+MFN9aj6L46Rv/go2fyoNjtgM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Numerics.wasm",
        "name": "System.Numerics.w9sdg2myku.wasm",
        "hash": "sha256-ZAhFzKBLuaEJkgnZyMvbo8oBIYU1oY7pWxtfo/3zg4Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ObjectModel.wasm",
        "name": "System.ObjectModel.o49nwl5106.wasm",
        "hash": "sha256-+LHFvGnrKMcSMi+sCz3ls/7Y8KznT+IBB9FGgz3HCnk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.DataContractSerialization.wasm",
        "name": "System.Private.DataContractSerialization.l1wepxshem.wasm",
        "hash": "sha256-uXYok4ofkKh/+NRuBxkTDDA3yo7sgBJeLf+1bK1+lMI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Uri.wasm",
        "name": "System.Private.Uri.17wno6fz9h.wasm",
        "hash": "sha256-vaJ55eXDUo9PrCfYLiid4iEcArxzTRq0aERdG2wOub0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Xml.Linq.wasm",
        "name": "System.Private.Xml.Linq.6ndmellwxq.wasm",
        "hash": "sha256-koAzBx3YzD0rGdvPEhKBKpHm+b5E2ZuZlXmAj1bFgZk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Xml.wasm",
        "name": "System.Private.Xml.1znmymukzn.wasm",
        "hash": "sha256-I4P+SLG8x3OY+ffkuKnjFI69+1oRN53/dcu1clzUlWc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.DispatchProxy.wasm",
        "name": "System.Reflection.DispatchProxy.uhng0i33yv.wasm",
        "hash": "sha256-8lJBa5yMe9ocAPcf+u2FEMVvtvKlfpGtadJJ9qiA+RY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.ILGeneration.wasm",
        "name": "System.Reflection.Emit.ILGeneration.mki4j8yrd1.wasm",
        "hash": "sha256-BiohgywrtvUlQ9yweDjQ42xVwKvXvkRWqQ8oOS6AT/U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.Lightweight.wasm",
        "name": "System.Reflection.Emit.Lightweight.rwbaytvlax.wasm",
        "hash": "sha256-NX9QaiN1pdiDQUvzVp22sBxPq7qb1h286QrFavpuy3M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.wasm",
        "name": "System.Reflection.Emit.sioal9node.wasm",
        "hash": "sha256-pAN3h4XuXHIgJ8N41G135W2oE4YV8nEXX5Pwby0UT5k=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Extensions.wasm",
        "name": "System.Reflection.Extensions.j3sy1079ix.wasm",
        "hash": "sha256-vxPC8Lknhll6pYAa+m93AyDMrs5YBR6JzCmZUZtbWvU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Metadata.wasm",
        "name": "System.Reflection.Metadata.jppm5mm49v.wasm",
        "hash": "sha256-WTe8k2LAdrTBQ7WnMhCgrSw9Ec9lBDpDsJ9f9hdLrxg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Primitives.wasm",
        "name": "System.Reflection.Primitives.td0lyduqh2.wasm",
        "hash": "sha256-MQtgnTwTHMtaT/DvGALvcRBAjJyIDjtzGcOfjvDrd7E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.TypeExtensions.wasm",
        "name": "System.Reflection.TypeExtensions.bw6raolkdi.wasm",
        "hash": "sha256-Varexc6bfsick0m07M5v70eo7ESJJoe5x16sRVRFd6o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.wasm",
        "name": "System.Reflection.vtl64yeamf.wasm",
        "hash": "sha256-/Z9nzWOLfEe/X8opJD3DYvv9M4a9XlFDkVkiD9nL2Nw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Resources.Reader.wasm",
        "name": "System.Resources.Reader.v4f2264f3m.wasm",
        "hash": "sha256-4SzOk8ZsFz3dJqEv8Fxp7ovBvYMrh9eLR8tL6DEZHCw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Resources.ResourceManager.wasm",
        "name": "System.Resources.ResourceManager.wfbqycsq2f.wasm",
        "hash": "sha256-lPHfv+UMo6+cO5sShlE+9SjMel1I2JgTWBw/WbMZIAc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Resources.Writer.wasm",
        "name": "System.Resources.Writer.yomp7oq8px.wasm",
        "hash": "sha256-IqZrJO3pSEU3l8PLAHs6dHyhf6tBXW4Kd03oQlJX9/U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.CompilerServices.Unsafe.wasm",
        "name": "System.Runtime.CompilerServices.Unsafe.3fy8j39bry.wasm",
        "hash": "sha256-wXC73+lTf8SQZxg7fIN2JaI1aKh71Z5tuNtZLp+rjGI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.CompilerServices.VisualC.wasm",
        "name": "System.Runtime.CompilerServices.VisualC.lzt2t2jx25.wasm",
        "hash": "sha256-Ufl48Z38OOFBpwxI6Hg3TQlHVe5gMkgRAS6tgq45Sd4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Extensions.wasm",
        "name": "System.Runtime.Extensions.bllnsrspwd.wasm",
        "hash": "sha256-D7U74t7jbJRnfpa9v5g9GvG2xUimYX/yBzd+nTEef5w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Handles.wasm",
        "name": "System.Runtime.Handles.l1doztbysk.wasm",
        "hash": "sha256-9+ASatqkwUS5JQeNsDnxqx7qbEjguRM/mj0dnx5TpsY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.RuntimeInformation.wasm",
        "name": "System.Runtime.InteropServices.RuntimeInformation.9rm6cit8v0.wasm",
        "hash": "sha256-tZytQU9Ot9cWvPshR0e0K8sL/w8AP6NwtvIIGULv0SI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.wasm",
        "name": "System.Runtime.InteropServices.92mbh6ougs.wasm",
        "hash": "sha256-jzLv11RwmdCK+0szbQUORqhqWV4qbY/fJXbFKK1jn6c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Intrinsics.wasm",
        "name": "System.Runtime.Intrinsics.m5kkftjqir.wasm",
        "hash": "sha256-ClZynzaqD2k+36tnRxpIMIgLWf3mFLGMeiPJsNCky+s=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Loader.wasm",
        "name": "System.Runtime.Loader.8tidl2vik3.wasm",
        "hash": "sha256-hHJDGEgMAWr1s+qiW1BrV+1Z7exLoySo/6fOWJqrd3A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Numerics.wasm",
        "name": "System.Runtime.Numerics.ym3nhl4pae.wasm",
        "hash": "sha256-1ui5OHMJ7wPS8CMagRQST8GZ+0fbZ/9pltkCoT+90pM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Formatters.wasm",
        "name": "System.Runtime.Serialization.Formatters.jfoer9a9b1.wasm",
        "hash": "sha256-0e07W2MjfznHKpgHXH4mF/uPkhswpfke9rEm8SzrIpc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Json.wasm",
        "name": "System.Runtime.Serialization.Json.04ot9ggpin.wasm",
        "hash": "sha256-sLc2UXv86/7spmjl9k/FlzZJtgLB91oVoAjVSMmI64A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Primitives.wasm",
        "name": "System.Runtime.Serialization.Primitives.v0sloygu0k.wasm",
        "hash": "sha256-4R6za3QhAG738JhwTSlKYmmVUewZL3+JEiy2EBTBZkY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Xml.wasm",
        "name": "System.Runtime.Serialization.Xml.l9p36z8pw0.wasm",
        "hash": "sha256-qZRNlDomw6MJdamd4o5xAXyVc89w8VvVilN0uYK+IYU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.wasm",
        "name": "System.Runtime.Serialization.sl1f80ke8z.wasm",
        "hash": "sha256-4H3w8zpYK/k5VcLN3klMmYqX9gmmcmLJDWGp/xNzDS0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.wasm",
        "name": "System.Runtime.6x7t8axmdw.wasm",
        "hash": "sha256-Ws0TMOkKaNQMKuS+Fshtw+92XVxaFCimL/bkvDLq9DU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.AccessControl.wasm",
        "name": "System.Security.AccessControl.3nwl7fw3mc.wasm",
        "hash": "sha256-gaQKfUFeWC/yC+vIXYGwISG7E0h347ZKZFxR/rMFCwc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Claims.wasm",
        "name": "System.Security.Claims.xz90ei9jt5.wasm",
        "hash": "sha256-ndi9w48lPV0EGw6ddNHDBcPoYQbPWSE3/SEuHecjHoc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Algorithms.wasm",
        "name": "System.Security.Cryptography.Algorithms.p2act7isih.wasm",
        "hash": "sha256-/2jRTOBlxI2qAsJecgUlKA7HPIDGfC4FSSyvPvlXduo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Cng.wasm",
        "name": "System.Security.Cryptography.Cng.x43abjiulr.wasm",
        "hash": "sha256-nxpZpScv6JOfnBL71rSVmbIJSvAulY3TLi8FsHGXimo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Csp.wasm",
        "name": "System.Security.Cryptography.Csp.ujvbo6nowk.wasm",
        "hash": "sha256-hivAoL3huWK/hEsopqiDNzD2Q6LaTS0V13mwDvU45zA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Encoding.wasm",
        "name": "System.Security.Cryptography.Encoding.87lifboqsx.wasm",
        "hash": "sha256-LElRZ2Qff7na2E+Ht9jLV7zIfkfGWk5ZGEe6X9rwEYM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.OpenSsl.wasm",
        "name": "System.Security.Cryptography.OpenSsl.u2qntds71c.wasm",
        "hash": "sha256-xvSMbbP5mYs78WPxfabMxS6CEiVXXW/z8DHAd06Ta9w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Primitives.wasm",
        "name": "System.Security.Cryptography.Primitives.ei71q5x4j1.wasm",
        "hash": "sha256-uoVej/K2vISCzdMszNTRCw8JHuzCyCjDp8gZotB5KZE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.X509Certificates.wasm",
        "name": "System.Security.Cryptography.X509Certificates.2spowjj5l2.wasm",
        "hash": "sha256-fqvts/KgaVC7h71J3M5xks4Qm3qnemRlLg9paDnsBm8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.wasm",
        "name": "System.Security.Cryptography.na4q58ou4y.wasm",
        "hash": "sha256-P91IL4b6yQRR9l71weSqfJ9rOXTRBEZI4DHXNPgKQTo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Principal.Windows.wasm",
        "name": "System.Security.Principal.Windows.w9a1l2myly.wasm",
        "hash": "sha256-REeNMiKakK0i+LDpQR0G3oIO8RHhAlL2UI+0CW7mxNE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Principal.wasm",
        "name": "System.Security.Principal.bh1imzrvgv.wasm",
        "hash": "sha256-AXZeh8L/uen1b1P66TzL/H/2MUPuRm/CSLFrXqLfrDE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.SecureString.wasm",
        "name": "System.Security.SecureString.x4k3li7xsu.wasm",
        "hash": "sha256-TwpwRnrNs27dbje6sMEyhXmdicvOpBted27udig6sTI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.wasm",
        "name": "System.Security.xcu685weif.wasm",
        "hash": "sha256-MZseoEAYl49td4OBMPYnTVFqg4ab7Q4lhtQKSYVjRfM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ServiceModel.Web.wasm",
        "name": "System.ServiceModel.Web.bj3h2scdlc.wasm",
        "hash": "sha256-J16MH06/hNRpUncDtgqLUlkzHraiCdEChHC3tlESOG0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ServiceProcess.wasm",
        "name": "System.ServiceProcess.5a1uwxr095.wasm",
        "hash": "sha256-/VUsU+8LpVIGBsJ1in3k4k+oT2Ge2My8YuSWCIQATCg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.CodePages.wasm",
        "name": "System.Text.Encoding.CodePages.bxr2gc97io.wasm",
        "hash": "sha256-IfZW7z5urfuADmp1nHcnlH+MShrRjbc8KxlXi9SqOOM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.Extensions.wasm",
        "name": "System.Text.Encoding.Extensions.ar7spvhfea.wasm",
        "hash": "sha256-uoSlDj/RP4D2q3z3Mw3VrUm19I7Fo7xOZfT5XzvpXyk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.wasm",
        "name": "System.Text.Encoding.z1tsijp8tc.wasm",
        "hash": "sha256-F5CjAxi2QCwntYK4j4UOtDegwSjxrZS4XYVZMFAHuhQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encodings.Web.wasm",
        "name": "System.Text.Encodings.Web.ecvfozax9d.wasm",
        "hash": "sha256-kk8Pc1Ur6Uul8F3nt+GPXzvBTnzgSIxM4vdWNJQbU+Q=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Json.wasm",
        "name": "System.Text.Json.t2y3tht3ok.wasm",
        "hash": "sha256-u3/5pDXPp1eZr4wgxyiFy0TOQgg8ibzjBJG/Ed7YQ4M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.RegularExpressions.wasm",
        "name": "System.Text.RegularExpressions.qwxrrj2acp.wasm",
        "hash": "sha256-llRhUd8PtG73MH2dI6tpZGZxISRKAn4l/bDru8ma/nw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.AccessControl.wasm",
        "name": "System.Threading.AccessControl.9ykh0d964r.wasm",
        "hash": "sha256-UeiDPJNkvktS+ZHXy5yEvXt6rnrqX4PxuwzgrHFmnS8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Channels.wasm",
        "name": "System.Threading.Channels.kqljytjj5u.wasm",
        "hash": "sha256-zNpzMcNMjctVEjN+LqhOMsphJ2pOVztYiWXaOO+25ZI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Overlapped.wasm",
        "name": "System.Threading.Overlapped.aw0j9ufvn4.wasm",
        "hash": "sha256-tuNpEI8jQs7rbsph7idVqHCnCY/pzMmZRqoLnGGMvZo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.Dataflow.wasm",
        "name": "System.Threading.Tasks.Dataflow.gsdu90uund.wasm",
        "hash": "sha256-sFWZt9uAGjanOJVHa7llIVf5qVeKRIp1FFo2UfHLZx8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.Extensions.wasm",
        "name": "System.Threading.Tasks.Extensions.bxz0ahfwn3.wasm",
        "hash": "sha256-ISzNUJ/eFCKfXPMRBqZg+Rlqf2tXnLTOrIsIuaRAoSM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.Parallel.wasm",
        "name": "System.Threading.Tasks.Parallel.m6q3sy58px.wasm",
        "hash": "sha256-TltUJqcCWphq9MN5/g3hVFqm5phD5d5Y5ICxEgONuBU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.wasm",
        "name": "System.Threading.Tasks.vr7balnlm6.wasm",
        "hash": "sha256-G2p7e/b1thoMgqV+/VcmMWKgaVN3zSNfApUd54HG1PU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Thread.wasm",
        "name": "System.Threading.Thread.yoidgvo6hg.wasm",
        "hash": "sha256-4ov9QfcmtHwHRwpxCXAsKH4g3veXT8rI3FbsEW89Zm8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.ThreadPool.wasm",
        "name": "System.Threading.ThreadPool.ylzen93rxt.wasm",
        "hash": "sha256-xta6KYtN6poMtdBSQbH3CTNjayPc1HEaCXZnwC5v9F4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Timer.wasm",
        "name": "System.Threading.Timer.ai8c4yy4gy.wasm",
        "hash": "sha256-7t2V9QZ4oI7StPrXJhUOQ1iQnRNqIiLKjRi4uVRFQOs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.wasm",
        "name": "System.Threading.7dgt1d3mf6.wasm",
        "hash": "sha256-G51WQ7j03lstpVgFIS/Rgx9Ei7vwZLqoTddlzUCBzaE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Transactions.Local.wasm",
        "name": "System.Transactions.Local.lzy5oi4lqx.wasm",
        "hash": "sha256-YWBAk4+509RyG20xmF8GrmkC4sigz0LUX6jWck0sFYk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Transactions.wasm",
        "name": "System.Transactions.t2zd200496.wasm",
        "hash": "sha256-K8uoLN/UN2qdfmJT9XTYrNxmI2VoUC2Jrqoc534LSSI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ValueTuple.wasm",
        "name": "System.ValueTuple.7wn6he28cp.wasm",
        "hash": "sha256-d4etbGmiQtcQ25lQP+/DjZsS1rqs5vw2JVObdI8IMXc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Web.HttpUtility.wasm",
        "name": "System.Web.HttpUtility.6tp4nz4hbm.wasm",
        "hash": "sha256-qr42CBau61hV8Bc9xTDtRzHthtHhg/xit/+HAsdTiqE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Web.wasm",
        "name": "System.Web.ebdit25xqb.wasm",
        "hash": "sha256-FteOHi1+0r+SGe0svuqML4XCBC3B/PiupXgkQFjqB/A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Windows.wasm",
        "name": "System.Windows.aq6lwwfr3z.wasm",
        "hash": "sha256-UmXZ2xzXZIwSArLJnHQKjfyFNH56w4sRe4Z4egHlwxg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.Linq.wasm",
        "name": "System.Xml.Linq.b7gdbqfifs.wasm",
        "hash": "sha256-uWnWhnYQtarrd5PsokR9QKk0fsUwI4oFad4rx0VBQGM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.ReaderWriter.wasm",
        "name": "System.Xml.ReaderWriter.y98l7nfpbl.wasm",
        "hash": "sha256-2o8zaXKRoPKOFJ7IrUCXwSbXzm77CYg+jqnzkXYm2qM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.Serialization.wasm",
        "name": "System.Xml.Serialization.7yexjpd5e3.wasm",
        "hash": "sha256-79DtH9ut2lkny/R8gyJGHraPPQzvgEH5tRXB5wmh064=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XDocument.wasm",
        "name": "System.Xml.XDocument.vg9k1j0zjc.wasm",
        "hash": "sha256-EQYoTyz3fqjCQ/5iFxAnoR81oKunRdddg6LZdVAqMWk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XPath.XDocument.wasm",
        "name": "System.Xml.XPath.XDocument.kwdgho41kx.wasm",
        "hash": "sha256-nMReov8iBoHa8IEuE+MUl62tVe9irNcWbeLVIy44lXw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XPath.wasm",
        "name": "System.Xml.XPath.b1ds872h3h.wasm",
        "hash": "sha256-/xTzME6qvVBigmgDbmVEVp/FiIcVmiqdcZAJkU11zLY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XmlDocument.wasm",
        "name": "System.Xml.XmlDocument.7obbgjtztj.wasm",
        "hash": "sha256-KSTTCxTwFkS8RtZg1NdpyxnM94/yUsOywQjy4XTa6kU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XmlSerializer.wasm",
        "name": "System.Xml.XmlSerializer.vgjr3kbu2b.wasm",
        "hash": "sha256-sYHb48y2hJbaWT2iDShSJefi0STSVUdV/ipwhbIZo+o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.wasm",
        "name": "System.Xml.4dgsvowdwq.wasm",
        "hash": "sha256-KJ9OjBdfB9r+m4yEC31C17mLDPbgDqHCknx0g2IACwQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.wasm",
        "name": "System.bv0vfagjd5.wasm",
        "hash": "sha256-J62Baw7gfV8sK5XwURj1GDN87DYteBFag2haGMDvA6M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "WindowsBase.wasm",
        "name": "WindowsBase.fin39dxgo4.wasm",
        "hash": "sha256-x6awrG0PK6N88ErLKZtswG2jfcmeesJojz0dPh3MvME=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "mscorlib.wasm",
        "name": "mscorlib.83s5ln6r4i.wasm",
        "hash": "sha256-dC5aSPpW8ylC2bmsSpcll/w4ThaEhnL/KEwOF3TQQcw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "netstandard.wasm",
        "name": "netstandard.qv9s71s9sf.wasm",
        "hash": "sha256-+tPP7i1i4Q/boVeZgA7ptN+InVDWoJbxot5eS/i9fqY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Actors.Abstractions.wasm",
        "name": "Aero.Actors.Abstractions.tuapn1cs22.wasm",
        "hash": "sha256-yyEo8hfg89DHzQH3d9F2hecoWjOzoZDmqH3uqsLxLZY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Actors.wasm",
        "name": "Aero.Actors.idta7pej83.wasm",
        "hash": "sha256-Nr4qljzz6M1XcvgVLsGF2m7Lp8Cvq8Vk/82vg/Nlyno=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Abstractions.wasm",
        "name": "Aero.Cms.Abstractions.xankuka56y.wasm",
        "hash": "sha256-+UAwUZ/ww85XtUy6ebgFdxATyI4P5vR015+yeJnoEsU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Contracts.wasm",
        "name": "Aero.Cms.Contracts.p3hsmd0v5o.wasm",
        "hash": "sha256-lS8BHB7Q0nQiBDGbShHZgElIwfiokn8jsMdPx3C6PGM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Html.wasm",
        "name": "Aero.Cms.Html.tv62v4zeh5.wasm",
        "hash": "sha256-WU7i5aF0lv9FKJtp/j7UPRTGaxzZbL/9BP6EgWld4t0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Modules.Commerce.Client.wasm",
        "name": "Aero.Cms.Modules.Commerce.Client.dadieskhvb.wasm",
        "hash": "sha256-O64nBVz7JdeLZHuaP7prfv4vxDoGv6DpvHJimtuJ9d0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Shared.wasm",
        "name": "Aero.Cms.Shared.urs30awo84.wasm",
        "hash": "sha256-ahydjOplM/0K3DsOhs7EqRfY4tGb5iPXfICgsWF/2bE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Core.Ai.wasm",
        "name": "Aero.Core.Ai.31kbisga4z.wasm",
        "hash": "sha256-2QNhaC4lmYH91QOVf9htEd5qyryIawUul0CaJZs0onQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Core.wasm",
        "name": "Aero.Core.jbzap6hktk.wasm",
        "hash": "sha256-sY0tCQCBzwrdmbnx5JaA8pf+qZAg3gG6l3cqZBPGHtA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Events.wasm",
        "name": "Aero.Events.daa42jwvde.wasm",
        "hash": "sha256-FTuBvzfzc1Ef+2yPJiSI/cY+LchpZ3HYfjfx2c/sfU8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Models.wasm",
        "name": "Aero.Models.k5li5i890v.wasm",
        "hash": "sha256-aFfuqwrUeHDlo1/bJc2jLedpWdDsF7aq5S4SIwbksS4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.DotNet.HotReload.WebAssembly.Browser.wasm",
        "name": "Microsoft.DotNet.HotReload.WebAssembly.Browser.r0uvx0ebhi.wasm",
        "hash": "sha256-RfVSLzqQEeeGURXg5fCxB/7pjzOLGNHC2cDbIQC7thQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Web.Client.wasm",
        "name": "Aero.Cms.Web.Client.2pya97atrx.wasm",
        "hash": "sha256-JtPboEnq//40Ej9KXYDA12x5ituC1wQlRTFHsgJdn/E=",
        "cache": "force-cache"
      }
    ],
    "pdb": [
      {
        "virtualPath": "Aero.Cms.Abstractions.pdb",
        "name": "Aero.Cms.Abstractions.nh3d9royqb.pdb",
        "hash": "sha256-FbgcVcpIRGevYnMWNlOLRFylcQ5P0Efg573MACqgUsA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Contracts.pdb",
        "name": "Aero.Cms.Contracts.fx8oboxtom.pdb",
        "hash": "sha256-zSF3Fd4efoPCoKModLZJc5wWmQYV8/DgSKGhvbA9uWE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Modules.Commerce.Client.pdb",
        "name": "Aero.Cms.Modules.Commerce.Client.z8518o6gq1.pdb",
        "hash": "sha256-05j51PRbcHcfJTHon7J0sNbxpNLk/llEXFgGOv+vUpQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Shared.pdb",
        "name": "Aero.Cms.Shared.0zsulr0f6m.pdb",
        "hash": "sha256-LC9D073GzslGKLKaJh/kZwLNFzQKhElXIS1p5bEWqHQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Actors.pdb",
        "name": "Aero.Actors.hkfzc83lcx.pdb",
        "hash": "sha256-b6q1LhqMeLK/jmN918Ux2do1Oc5Nqv6xIPMts/owq0M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Actors.Abstractions.pdb",
        "name": "Aero.Actors.Abstractions.4ybf34hmkr.pdb",
        "hash": "sha256-xBLU/i+58kPSr6h2zNa0di72l+jNnT8Cwf632CBwYMI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Html.pdb",
        "name": "Aero.Cms.Html.r7knmms2j8.pdb",
        "hash": "sha256-6Qv88VrytX2NajpzDbFqitJCjsDPstNmqtPWFlG/Pvk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Core.pdb",
        "name": "Aero.Core.8fl76y86al.pdb",
        "hash": "sha256-NPbE2it790ZAoQUk4+Z19wloy6NuHcGlsrGjPk49oxU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Core.Ai.pdb",
        "name": "Aero.Core.Ai.um7rbzw400.pdb",
        "hash": "sha256-mkkDYAXNoeaOjCO5srsjmBE+5ng62Q9itKYhLXYzlCs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Events.pdb",
        "name": "Aero.Events.nzou1umffw.pdb",
        "hash": "sha256-/SwBYCqFNaD/QdH6NT9/uWWJkgr8ShfX+FqDgjsrcUk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Models.pdb",
        "name": "Aero.Models.31ez6gnqwz.pdb",
        "hash": "sha256-xydGlEPQ+DFVNzOiWY+aa6AVrYN8dqCAUquSkslgjaI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Aero.Cms.Web.Client.pdb",
        "name": "Aero.Cms.Web.Client.fz4baxe18b.pdb",
        "hash": "sha256-+3vcuZSztD4g7Da1ub9GbrjX46yHCj4frVL0UIqyxyU=",
        "cache": "force-cache"
      }
    ],
    "satelliteResources": {
      "cs": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.k2w3mhwa07.wasm",
          "hash": "sha256-5AAoE+VEEKjOKppvnO0NXWR+FfDFviAAz6dirSZXXkc=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.19kzaraf2j.wasm",
          "hash": "sha256-RUdTeKysikZvzJmLdfWhz8xz2vsYBT+WxCvM+zxhA+k=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.8d8qqfn5pl.wasm",
          "hash": "sha256-3FLLxuxJ5porgmR5Beew+qtr/Pu7ZE1dm4LluMSx6xo=",
          "cache": "force-cache"
        }
      ],
      "de": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.jrul1nigrc.wasm",
          "hash": "sha256-/ws8akfgIPA946chWhnykIj60oqzui+aI7PiBvYc9gc=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.fr7rfz9kts.wasm",
          "hash": "sha256-9TRG9OgsETX0LzlmmYw2KTrimnBR/pVdD4nTm983eC8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.m7u4v0mdtg.wasm",
          "hash": "sha256-8pDfLrCtZNZsYMi+0ssU2abvVsMsUTNcp5CDN3KM1ys=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Radzen.Blazor.resources.wasm",
          "name": "Radzen.Blazor.resources.m7ldtnud19.wasm",
          "hash": "sha256-osUXCndVjVwVJzPoZGVY+698ZKGW6WYDvH4lKnZCSK4=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.vcwncfqchi.wasm",
          "hash": "sha256-j8tPKjd/n48cTHsm50QSWKvSyidPnqCzo5BHOXrX+68=",
          "cache": "force-cache"
        }
      ],
      "es": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.gs17fo235q.wasm",
          "hash": "sha256-0LPqXNHrSpxy0auus32GhLiEWOrSpOk4X3dLIi0IdWo=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.8txpr7w59n.wasm",
          "hash": "sha256-lNHQdm2nofay47a5HX+Q5H/bT4EtLg4uOVIEilIPB0Q=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.l5stqa22do.wasm",
          "hash": "sha256-N2o+ICJVZN3FVT14VFt6G5ROd5Je1KtfVWBaudt5lF8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Radzen.Blazor.resources.wasm",
          "name": "Radzen.Blazor.resources.yiacyfwxe9.wasm",
          "hash": "sha256-tgGmxr8d74nn5u7lu0LIYS9I9oLkjJT0erzPOnncPv0=",
          "cache": "force-cache"
        }
      ],
      "fr": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.mw7bxyd1cx.wasm",
          "hash": "sha256-OmpuoghrvMmkX6qg0xZBDL/zieLnPaiUNXC537v5M7w=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.0lbw7v84lk.wasm",
          "hash": "sha256-pMUK2zqFZZojYeHAO+2dCWPDbz1/hNlDhbqZHoCZYkk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.3c2oyuj8lt.wasm",
          "hash": "sha256-LRTvFsoLnRDeS+ojaEvEd6NG2xq7X2O+HyeYuXUf9+E=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Radzen.Blazor.resources.wasm",
          "name": "Radzen.Blazor.resources.cm1pp94os0.wasm",
          "hash": "sha256-jG4PY5FEZKKYDckIV2l2Vd2wvP5uaB4Hzq2G1cbFzhc=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.ceqezcwe13.wasm",
          "hash": "sha256-3E/e3gxhWv2WIrR0ciapMHTngZqY2if90o2cUQ/crBk=",
          "cache": "force-cache"
        }
      ],
      "it": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.k2wkf7t3s8.wasm",
          "hash": "sha256-XFqlyThLxHRA7uL2blMZ5Y6Sl7Ftz8JOdtg+GTy8ItY=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.zt36s7opzu.wasm",
          "hash": "sha256-5zBFIBWPbjFeCLw7Xsd9ZB7wdNEGLrSeZMLM8g2G+Is=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.1n9qhnbjm7.wasm",
          "hash": "sha256-TZCBtlz2xE1/MBFFl7JpVsQXJ7fut8TjXDNMJP1jz48=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Radzen.Blazor.resources.wasm",
          "name": "Radzen.Blazor.resources.7cuwxpyd8e.wasm",
          "hash": "sha256-l4ZIQCIwgokdGC2J6rRUnfjv/NSqxGOu606HcDgpFd8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.y45x6ij60g.wasm",
          "hash": "sha256-QpbEyJIcRRYt9+D4AMQqv0ReAsFoXoHHJ4VVhW2pZF8=",
          "cache": "force-cache"
        }
      ],
      "ja": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.6ydrix4n4z.wasm",
          "hash": "sha256-XvVDR01x6RN6BdmeIa/vY/cGPgvqliwAHfqdDzSDJLk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.93fwyaqel7.wasm",
          "hash": "sha256-Zf4YQmMVCxgYGqnkz9QvqZEg2kMJjsIhFEobDw/0Azk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.cignb3mvv9.wasm",
          "hash": "sha256-rFWtLvZi0gXu/0YMvJx6GQRFupoU6QqwZwUNgHJmRfE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Radzen.Blazor.resources.wasm",
          "name": "Radzen.Blazor.resources.qm6n5ojlc8.wasm",
          "hash": "sha256-UnbF1kH0oOlmi1ThgvmGdQjitbaYiK8kBvKZgHuNRxw=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.ez1lyyvbuj.wasm",
          "hash": "sha256-Std5FrYvZUwc6Zngdhf47tL4Fy9dXQdObbkhgYrTNB0=",
          "cache": "force-cache"
        }
      ],
      "ko": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.b1sticklb0.wasm",
          "hash": "sha256-L7VVAX3VTvIgZU474lTeBHgiuQxYl6NmSzRF+cmUjnk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.5ocsmx07ez.wasm",
          "hash": "sha256-JelEuFlE08AMeqsWE1PR9+jwtieO38o8hEo9z/wC39s=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.l5zjlwtgz5.wasm",
          "hash": "sha256-R0LTFVgMjImhUROrPfUYGzQga9Te58JRpDC6zT5Oggk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.w02kv8nsfr.wasm",
          "hash": "sha256-QFZifzp5MtIfr90sxSleWLVPWYL4g9SZnSqrIZTf6lo=",
          "cache": "force-cache"
        }
      ],
      "pl": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.aozvshmubu.wasm",
          "hash": "sha256-ptmMN4yy7w33vNswuxcAGfRZ3bU+fb+OPLTEYaDYWMc=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.kddnrje2p6.wasm",
          "hash": "sha256-eCr/kTEM/lEQgRTKfKtnQpXvPZM7RH7sFTQtP7yglW0=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.bu6p7wln2r.wasm",
          "hash": "sha256-4+XrxEtH6fhazpnixmhkXiVYhJj9ehdcyIsYA39RbFc=",
          "cache": "force-cache"
        }
      ],
      "pt-BR": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.zv156ps1rf.wasm",
          "hash": "sha256-MZ2Nk6Px9uy5e2UyGvLKtsWhfNoJpEhTF2kz0OiBbZ4=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.7y97jvbmw3.wasm",
          "hash": "sha256-4dJOQ+FOZPLjj7cMIF6s5MlmNRJOjmLaUPO/Nn8MF7U=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.bjx8l78r3y.wasm",
          "hash": "sha256-ObMgq9Omt2nOyDlA9bMQgMRm2un6iwShG3U0dClReKo=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.thrbekfuio.wasm",
          "hash": "sha256-8W0DVlqvNE5lPnjWwglDJmdGkABj+25oByKGoZxVf0o=",
          "cache": "force-cache"
        }
      ],
      "ru": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.jrtuwdet4j.wasm",
          "hash": "sha256-0Xnb8+I6FK+UeuOIXmECjBa2JXg6A242nqE/3WxwJ1k=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.5cfnmmqxmq.wasm",
          "hash": "sha256-ZXvqXJrbrtpzoDczhp+ACXH8FidftfRPKcEArQwL3ko=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.3ai7snazjs.wasm",
          "hash": "sha256-SxBErB8JhS8iPbv3DvXCufXxuF0IlFMInim4Zu25BhE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.csmgzijh74.wasm",
          "hash": "sha256-XNKDEjOntSdECfrPc88A3OsX+Oda+9kRC8ngM5QJ8QU=",
          "cache": "force-cache"
        }
      ],
      "tr": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.supgpk80d0.wasm",
          "hash": "sha256-HBDpI1dSfTKUI/LMl9LtFInbN7GMcHa0u+/Q26DeFGA=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.uphkrecjin.wasm",
          "hash": "sha256-TvYmqtEe4SHWyXEOsAD3XkdfNFV4cvB0eac/AmDHE9Q=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.i3l4m4hr8p.wasm",
          "hash": "sha256-znmO0cSXgjhk/UlYVbt0IwF6iuG6ZBE3pYiLjyMIEFE=",
          "cache": "force-cache"
        }
      ],
      "zh-Hans": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.uoqbwu4d5d.wasm",
          "hash": "sha256-3jN67QIzaCYlN25M3byvmhrUypuE8F57E786mKscmB8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.7horqp1cux.wasm",
          "hash": "sha256-Vd3bxvKNtYGElc5RwOQP4bi9g2XzFt6OAu0l1EhrE/Q=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.4t1z8ntbeh.wasm",
          "hash": "sha256-GLgReaoIBrDL+dZ1jYk8+HOawN+DMCloIVKajysY8D4=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.ywpha019f8.wasm",
          "hash": "sha256-jshECHPWVevorimUcqnyenbRIjz6SEetu/I+x7fpA0E=",
          "cache": "force-cache"
        }
      ],
      "zh-Hant": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.wasm",
          "name": "Microsoft.CodeAnalysis.resources.cw5kdl8sc3.wasm",
          "hash": "sha256-3Hv6IJP0lj56wSIUL0jYPDINIz1sLup1I5Tt9uwIlGU=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.wasm",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.hz9zb8k7nm.wasm",
          "hash": "sha256-s9fnrOJAkwfS27dcYcdKwBrg2SNKfPa97nG8zVjmqGU=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.Data.SqlClient.resources.wasm",
          "name": "Microsoft.Data.SqlClient.resources.wr9fjicf1c.wasm",
          "hash": "sha256-xCXzFes0V+jJCiO5Aswp/8XV/WH8y5d7u1PXKjXYGP8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.pb4vv8vdvn.wasm",
          "hash": "sha256-pfYMohnRVz801kfoMyHfmb0oN5LnnbonEL95oiFu6sg=",
          "cache": "force-cache"
        }
      ],
      "de-DE": [
        {
          "virtualPath": "SecretSharingDotNet.resources.wasm",
          "name": "SecretSharingDotNet.resources.t1yxjr64mo.wasm",
          "hash": "sha256-ocn36BEsr6ULPPDXsMvblyBiTEcNKEWC0fDLgMPUcuE=",
          "cache": "force-cache"
        }
      ],
      "da": [
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.s7shujkhci.wasm",
          "hash": "sha256-6MwEGMVAJ9vQ+W/vcBrjfYfEsuwJo4TP+0EJqJX0oeg=",
          "cache": "force-cache"
        }
      ],
      "es-MX": [
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.0eoai8zrul.wasm",
          "hash": "sha256-CARUbg9F0DnccktR2h3sNMlpSEOctEB61da+SR6lKlA=",
          "cache": "force-cache"
        }
      ],
      "hi-IN": [
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.1jpxk57m8l.wasm",
          "hash": "sha256-g0jDxU8ovBeJ28GW4A88ZPkr2YWiNHbq9ubVg/0/+Y0=",
          "cache": "force-cache"
        }
      ],
      "nl": [
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.yirfse7clj.wasm",
          "hash": "sha256-5sJsI04xvh+HbY+RL51yi2ngfC1+O81GRWHBJKO8z24=",
          "cache": "force-cache"
        }
      ],
      "sv": [
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.e44evutxrc.wasm",
          "hash": "sha256-XtOZSwOVBusGePrbPFvQUjDA5QgrC4GwJ5k7zSXgzoE=",
          "cache": "force-cache"
        }
      ],
      "uk": [
        {
          "virtualPath": "Aero.Cms.Shared.resources.wasm",
          "name": "Aero.Cms.Shared.resources.xis0n1z5oc.wasm",
          "hash": "sha256-aRvHEaTqDhl/ycwAHvr/VQmTm8drXpzuGHC2bKt9u58=",
          "cache": "force-cache"
        }
      ]
    },
    "libraryInitializers": [
      {
        "name": "_framework/Microsoft.DotNet.HotReload.WebAssembly.Browser.99zm1jdh75.lib.module.js"
      }
    ],
    "modulesAfterConfigLoaded": [
      {
        "name": "../_framework/Microsoft.DotNet.HotReload.WebAssembly.Browser.99zm1jdh75.lib.module.js"
      }
    ]
  },
  "debugLevel": 0,
  "appsettings": [
    "../appsettings.Development.json",
    "../appsettings.json"
  ],
  "globalizationMode": "all",
  "extensions": {
    "blazor": {}
  },
  "runtimeConfig": {
    "runtimeOptions": {
      "configProperties": {
        "Microsoft.AspNetCore.Components.Routing.RegexConstraintSupport": false,
        "Serilog.Capturing.IsStructureValueSupported": false,
        "Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability": true,
        "System.ComponentModel.DefaultValueAttribute.IsSupported": false,
        "System.ComponentModel.Design.IDesignerHost.IsSupported": false,
        "System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization": false,
        "System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported": false,
        "System.Data.DataSet.XmlSerializationIsSupported": false,
        "System.Diagnostics.Metrics.Meter.IsSupported": false,
        "System.Diagnostics.Tracing.EventSource.IsSupported": false,
        "System.GC.Server": true,
        "System.Globalization.Invariant": false,
        "System.TimeZoneInfo.Invariant": false,
        "System.Linq.Enumerable.IsSizeOptimized": true,
        "System.Net.Http.EnableActivityPropagation": false,
        "System.Net.Http.WasmEnableStreamingResponse": true,
        "System.Net.SocketsHttpHandler.Http3Support": false,
        "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
        "System.Resources.ResourceManager.AllowCustomResourceTypes": false,
        "System.Resources.UseSystemResourceKeys": true,
        "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported": true,
        "System.Runtime.InteropServices.BuiltInComInterop.IsSupported": false,
        "System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting": false,
        "System.Runtime.InteropServices.EnableCppCLIHostActivation": false,
        "System.Runtime.InteropServices.Marshalling.EnableGeneratedComInterfaceComImportInterop": false,
        "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
        "System.StartupHookProvider.IsSupported": false,
        "System.Text.Encoding.EnableUnsafeUTF7Encoding": false,
        "System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault": true,
        "System.Threading.Thread.EnableAutoreleasePool": false,
        "Microsoft.AspNetCore.Components.Endpoints.NavigationManager.DisableThrowNavigationException": false
      }
    }
  }
}/*json-end*/);export{gt as default,ft as dotnet,mt as exit};
