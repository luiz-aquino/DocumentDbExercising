/*
    Function that maintains Entity hierarquie maitaining the leaves with the relevant data
    and the root with all the information
*/
function upsertEntity() {
    var context = getContext();
    var collection = context.getCollection();
    var collectionLink = collection.getSelfLink();
    var entity = context.getRequest().getBody();

    if(entity.baseId) {
        updateTree(copyValues);
    }
    else {
        createTree(copyValues);
    }

    function createTree(copyValues) {
        var id = collection.generateGuidId();
        newEntity.baseId = id;

        if(entity.parents && entity.parents.length) {
            for(var i = 0; i < entity.parents.length; i++){
                var newEntity = copyValues(entity, {});
                newEntity.parents = [ entity.parents[i] ];
                var isAccepted = collection.createDocument(collectionLink, newEntity, function(err){ if(err) throw err;  });
                if(!isAccepted) throw new Error("A criação do documento foi negada do parent (" + parent.id + ") foi negada;");
            }            
            entity.parents = [];
        }

        entity.id = id;
        entity.isBase = true;
        var isAccepted = collection.createDocument(collectionLink, entity, function(err){ if(err) throw err;  });
        if(!isAccepted) throw new Error("A criação do documento foi negada");

        context.getResponse().setResponse().setBody(entity);
    }

    function updateTree(copyValues) {
        if(entity.parents && entity.parents.length){
            var result = __.filter(function(doc) { doc.baseId === entite.baseId },
                , function(err, feed, options){
                    if(err !== null) throw err;        
                    for(var i = 0; i < feed.length; i++){
                        var curr = feed[i];

                        if(!curr.isBase){
                            if(!curr.parents || curr.parents.length) continue;
                            var exists = false;
                            for(var j = 0; j < curr.parents.length; j++){
                                for(var z = 0; z < entity.parents; z++){
                                    exists = exists || entity.parents[z].id === curr.parents[j].id;
                                    if(exists) break;
                                }
                                if(exists) break;
                            }
                            if(!exists) continue;
                        }

                        copyValues(curr, entity);

                        var isAccepted = collection.replaceDocument(curr._self, curr, function (err) { if(err) throw err; });
                        if(!isAccepted) throw new Error("A chamada de atualização do documento não foi aceita");
                    }
            });
        
            if(!result.isAccepted) throw new Error("A chamada para filtrar as entidades foi negada");
        }
        else {
            var result = __.filter(function(doc) { return doc.id === entity.id }, function(err, feed, options){
                if(err) throw err;
                var doc = feed[0];
                copyValues(doc, entity);
                var isAccepted = collection.replaceDocument(doc._self, doc, function (err) { if(err) throw err; });
                if(!isAccepted) throw new Error("A chamada de atualização do documento não foi aceita");
            });
        }
    }

    function copyValues(docA, docB) {
        var objs = [docB];
        for(var prop in docA){
            if(!docB[prop]) continue;
            
            if(docA[prop].constructor === Object || doA[prop].constructor === Array){

                if(objs.indexOf(docB[prop]) > -1) continue;
                
                objs.push(docB[prop]);                
                copyValues(docA[prop], docB[prop]);
                
                continue;
            }

            docA[prop] = docB[prop];
        }
    }
}