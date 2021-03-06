﻿/// <reference path="../_references.ts" />
module app.controllers {
    export class EntityEditController extends app.base.ControllerBase {
        public static $inject = ["$scope", "$stateParams", "$state", "$debounce", "dialogs", "EntityService"];
        
        private groupService = new app.services.MetadataDefinitionGroupService();
        
        constructor($scope: ng.IScope, public $stateParams, public $state, public $debounce, public $dialogs, public entityService: app.services.EntityService) {
            super();

            var duplicate = $state.current.data.duplicate;

            var scope = {
                title: "",
                originalName: "",
                editMode: false,
                cancel: (e) => {
                    e.preventDefault();
                    app.redirectToRoute(app.Routes.entities);
                },
                model: <app.model.EntityResource>null,
                groupSelect2: <Select2Options>{
                    ajax: {
                        url: this.groupService.getSearchUrl(),
                        data: (term, page) => {
                            return { q: term, pageSize: 10, page: page };
                        },
                        results: (data, page) => {
                            return { results: data, more: data.length >= 10 };
                        }
                    },
                    initSelection: (data, callback) => {
                        callback(scope.model.definitionGroup);
                    }
                },
                regex: (def: app.model.EntityMetadataDefintionResource) => {
                    return new RegExp(def.regex);
                },
                doDelete: () => {
                    //var dialog = app.resolveService<app.services.Dialog>(app.services.Dialog.prototype);
                    var msg = 'Are you sure you wish to delete this item?';
                    //var confirm = window.confirm(msg);
                    ////var confirm = dialog.showModal('Yes', 'No','Are you sure you wish to delete this item?');
                    //if (!confirm) return;

                    var opts: any = {};
                    opts.kb = true;
                    opts.bd = true;
                    opts.ws = 'sm'; // values: 'sm', 'lg', 'md'

                    var dlg = this.$dialogs.confirm('Confirm', msg, opts);
                    dlg.result.then(btn => {
                        app.fn.spinStart();
                        entityService.doDelete(scope.model.identity)
                            .then(pro => {
                                    toastr.success(scope.originalName, "Deleted");
                                    app.fn.spinStop();
                                    app.redirectToRoute(app.Routes.entities);
                                    //  e.preventDefault();
                                },
                                (err => {
                                    app.log.error(err);
                                    var msg = '';
                                    angular.forEach(err.data.modelState, resource => {
                                            msg += resource + '\n';
                                        }
                                    );
                                    toastr.error(msg, "Error");
                                    app.fn.spinStop();
                                })
                            );
                    }, btn => {
                        app.log.info("Delete metadata definition group cancelled");
                    });
                },
                hasSelectedValue: (select2Value) => {
                    if (select2Value === undefined)
                        return false;

                    if ($.isArray(select2Value))
                        return select2Value.length > 0;

                    return select2Value.text != undefined && select2Value.text != '';
                },
                save: () => {
                    var hasSelectionValidationErrors = false;
                    for (var d in scope.model.definitionValues) {
                        var def: any = scope.model.definitionValues[d];
                        if (def.isSelection) {
                            if (!scope.hasSelectedValue(def.select2Value)) {
                                toastr.error(def.name, "No Value Selected for field");
                                hasSelectionValidationErrors = true;
                                continue;
                            }

                            if ($.isArray(def.select2Value)) {
                                def.values = $.map(def.select2Value, x => x.text);
                            } else {
                                def.values[0] = def.select2Value.text;
                            }
                        }
                    }
                    
                    if(hasSelectionValidationErrors)
                        return;

                    if (scope.editMode) {
                        scope.update();
                    } else {
                        scope.create();
                    }
                },
                create: () => {
                    app.fn.spinStart();
                    scope.model.identity = app.fn.createGuid();
                    entityService.create(scope.model)
                        .then(pro => {
                            toastr.success(scope.model.name, "Created");
                            app.fn.spinStop();
                            app.redirectToRoute(app.Routes.entities);
                        },
                        (err => {
                            app.log.error(err);
                            var msg = '';
                            angular.forEach(err.data.modelState, resource => {
                                    msg += resource + '\n';
                                }
                            );
                            if (msg.length > 0) {
                                toastr.error(msg, "Error");
                            } else {
                                toastr.error(err.data.message, "Error");
                            }
                            app.fn.spinStop();
                        }));
                },
                update: () => {
                    app.fn.spinStart();

                    entityService.update(scope.model, this.$stateParams.identity)
                        .then(pro => {
                            toastr.success(scope.model.name, "Saved");
                            app.fn.spinStop();
                            app.redirectToRoute(app.Routes.entities);
                        },
                        (err => {
                            app.log.error(err);
                            var msg = '';
                            angular.forEach(err.data.modelState, resource => {
                                    msg += resource + '\n';
                                }
                            );
                            if (msg.length) {
                                toastr.error(msg, "Error");
                            } else {
                                toastr.error(err.data.message, "Error");
                            }
                            app.fn.spinStop();
                        }));
                },
                originalValues: <app.model.EntityMetadataDefintionResource[]>[],
                toBeDeletedValues: <{ name: string; value: string }[]>[]
            };

            if ($stateParams.identity) {
                scope.editMode = !duplicate;
                entityService.get($stateParams.identity).then((res: any) => {
                    scope.model = res.data;
                    if (duplicate) {
                        scope.title = "Create new entity";
                        scope.model.name = "";
                    } else {
                        scope.title = scope.model.name;
                    }
                    scope.originalName = scope.model.name;
                    scope.originalValues = scope.model.definitionValues;
                });
            } else {
                scope.title = "Create new entity";
                scope.model = new app.model.EntityResource();
                scope.model.identity = app.fn.createGuid();
            }
            

            

            scope = $.extend($scope, scope);

            var loadGroup = (groupId: app.model.Guid)=> {
                if (groupId) {
                    var oldDefValues = $.extend([], scope.originalValues, scope.model.definitionValues);
                    scope.model.definitionValues = [];

                    this.groupService.getDefinitions(scope.model.definitionGroup.id).then((res: any)=> {
                        var definitions: app.model.MetadataDefinitionResource[] = res.data;
                        var definitionIds = $.map(definitions, d=> d.identity);

                        for (var i in definitions) {
                            var def = definitions[i];

                            var entityDef = new app.model.EntityMetadataDefintionResource;
                            entityDef.metadataDefinitionIdentity = def.identity;
                            entityDef.name = def.name;
                            entityDef.regex = def.regex;
                            entityDef.dataType = def.dataType;

                            for (var j in oldDefValues) {
                                if (oldDefValues[j].metadataDefinitionIdentity == def.identity) {
                                    entityDef.values = $.extend([], oldDefValues[j].values);
                                    break;
                                }
                            }

                            var isSelection = def.dataType === "ComboBox" || def.dataType === "PickList";
                            $.extend(entityDef, { isSelection: isSelection });

                            if (!entityDef.values) {
                                if (isSelection) {
                                    entityDef.values = [];
                                } else {
                                    entityDef.values = def.values;
                                }
                            }

                            if (isSelection) {
                                (()=> {
                                    var isMultiple = def.dataType === "PickList";
                                    var select2Value = $.map(entityDef.values, x=> { return { id: x, text: x }; });
                                    var val = isMultiple ? <any>select2Value : select2Value[0];
                                    var values = $.map(def.values, s=> { return { id: s, text: s } });

                                    var select2Options = <Select2Options>{
                                        multiple: isMultiple,
                                        initSelection: (data, callback)=> {
                                            console.log(val);
                                            callback(val);
                                        }
                                    }
                                    if (isMultiple) {
                                        select2Options.tags = values;
                                    } else {
                                        select2Options.data = values;
                                    }
                                    $.extend(entityDef, {
                                        select2Options: select2Options,
                                        select2Value: val
                                    });
                                })();
                            }

                            scope.model.definitionValues.push(entityDef);
                        }

                        scope.toBeDeletedValues = [];
                        for (var i in scope.originalValues) {
                            var defValue = scope.originalValues[i];
                            if ($.inArray(defValue.metadataDefinitionIdentity, definitionIds) < 0) {
                                scope.toBeDeletedValues.push({
                                    name: defValue.name,
                                    value: defValue.values.join(", ")
                                });
                            }
                        }
                    });
                }
            };



            $scope.$watch("model.definitionGroup.id", loadGroup);
        }
    }
}