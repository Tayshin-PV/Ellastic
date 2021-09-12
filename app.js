var mainApp = angular
    .module('elasticsearch', ['ngSanitize', 'ngMaterial', 'ngRoute', 'material.core', 'ui.bootstrap', 'ui.bootstrap.pagination'])
    .config(function ($locationProvider) {
        $locationProvider.hashPrefix('!');
        $locationProvider.html5Mode({
            enabled: false,
            requireBase: true
        });
    }
    )
    .config(
        [
            '$sceDelegateProvider',
            function ($sceDelegateProvider) {
                $sceDelegateProvider.resourceUrlWhitelist([
                    // Allow same origin resource loads.
                    //    'self',
                    // Allow loading from our assets domain.  Notice the difference between * and **.
                    'http://sng-pubsrp/**'
                ]);

                // The banned resource URL list overrides the trusted resource URL list so the open redirect
                // here is blocked.
                //$sceDelegateProvider.resourceUrlBlacklist([
                //    'http://sng-pubsrp/**'
                //]);
            }
        ]
    )
    .controller('searchController', function ($scope, $http, $location) {
        var dictonaryName = "dictonaryName";
        var activeWFilters = [];
        var getSuggestions = function (q) {
            var sg = $http.get("/api/suggest?q=" + encodeURIComponent(q));
            return sg;
        }
        $scope.switch2Page = 1;
        $scope.currentPage = 1;
        $scope.pageSize = 10;
        $scope.setPage = function (pageNo) {
            $scope.currentPage = pageNo;
        };
        $scope.maxSize = 5;
        var self = this;
        $scope.savedSearch = null; // optional declaration!
        $location.search('savedSearch', '123rtf');
        bind("savedSearch");
        function bind(valueName) {
            // Controller to URL
            $scope.$watch(function () { return self[valueName] }, function (newVal) {
                console.log("Property changed!");
                //$location.search(valueName, newVal);
            });
            // URL to controller
            $scope.$on('$locationChangeSuccess', function (event) {
                console.log("URL changed!");
                $scope.savedSearch = $location.search()[valueName];
            });
        }
        var startLoadingAnimation = function () // - функция запуска анимации
        {
            // найдем элемент с изображением загрузки и уберем невидимость:
            loadImg.style.display = "block";

            // вычислим в какие координаты нужно поместить изображение загрузки,
            // чтобы оно оказалось в серидине страницы:
            var centerY = loadImg.scrollTop + (window.innerHeight + loadImg.height) / 2;
            var centerX = loadImg.scrollLeft + (window.innerWidth + loadImg.width) / 2;

            // поменяем координаты изображения на нужные:
            loadImg.style.top = centerY + "px";
            loadImg.style.left = centerX + "px";
        }

        var stopLoadingAnimation = function () // - функция останавливающая анимацию
        {
            loadImg.style.display = "none";
        }

        var search = function () {

            //startLoadingAnimation();
            //stopLoadingAnimation();
            //router.navigate(['team', 33, 'user', 11], { relativeTo: route });
            //return;
            $http.get("/api/search?q=" + encodeURIComponent($scope.query) + "&dictonaryName=" + activeWFilters[dictonaryName] + "&page=" + $scope.switch2Page + "&pageSize=" + 10).then(function (response) {
                $scope.isLoading = false;
                $scope.items = response.data.Results;
                $scope.aggs = response.data.Aggregations;
                $scope.total = 0;
                $scope.currentPage = response.data.Page / $scope.pageSize + 1;
                if (response.data.Results == null)
                    if (response.data.Message !== "" && response.data.Message !== null)
                        $scope.message = response.data.Message;
                    else
                        $scope.message = "ничего не найдено";
                else {
                    $scope.message = null;
                    $scope.total = response.data.Total;
                    $scope.took = response.data.ElapsedMilliseconds;
                    $scope.searchID = response.data.SearchID;
                }
            });
            getSuggestions($scope.query).then(function (response) {
                $scope.suggested = response.data;
            });
        }

        $scope.isActive = function (t, value) {
            var index = activeWFilters.indexOf(t);
            if (index === -1) {
                return false;
            } else
                return activeWFilters[t].length > 0 && activeWFilters[t].indexOf(value) >= 0;
        }

        var searchByCategory = function () {
            var sActiveFilters = [];
            var index = activeWFilters.indexOf(dictonaryName);
            if (index === -1) {
                activeWFilters.push(dictonaryName);
            }
            index = activeWFilters[dictonaryName];
            activeWFilters[dictonaryName] = activeWFilters.dictonaryName;
            //if (activeWFilters.findIndex("dictonaryName") == -1)

            for (var i = 0; i < activeWFilters.length; i++) {
                sActiveFilters[i] = [];
                sActiveFilters[i][0] = activeWFilters[i];
                sActiveFilters[i][1] = activeWFilters[activeWFilters[i]];
            }
            $http.post("/api/searchbycategory", { "q": $scope.query, "esFilters": sActiveFilters, "page": $scope.switch2Page, "pageSize": $scope.pageSize }).then(function (response) {
                $scope.isLoading = false;
                $scope.items = response.data.Results;
                $scope.aggs = response.data.Aggregations;
                $scope.total = 0;
                $scope.currentPage = response.data.Page / $scope.pageSize + 1;
                if (response.data.Results == null)
                    $scope.message = "ничего не найдено";
                else {
                    $scope.message = null;
                    $scope.total = response.data.Total;
                    $scope.took = response.data.ElapsedMilliseconds;
                }
            });
            getSuggestions($scope.query).then(function (response) {
                $scope.suggested = response.data;
            });
        }

        $scope.togglePages = function (currentPage) {
            $scope.switch2Page = (currentPage - 1) * $scope.pageSize;
            if (activeWFilters.length > 0) {
                searchByCategory();
            } else {
                search();
            }
            $scope.switch2Page = 1;
        }
        $scope.toggleFilters = function (t, extension) {
            $scope.isLoading = true;
            if (activeWFilters.indexOf(t) === -1) {
                activeWFilters.push(t);
            }
            if ("undefined" === typeof activeWFilters[t]) {
                activeWFilters[t] = [];
            }
            var index = activeWFilters[t].indexOf(extension);
            if (index === -1) {
                activeWFilters[t].push(extension);
            } else {
                activeWFilters[t].splice(index, 1);
            }
            searchByCategory();
        }
        $scope.switchSource = function () {
            $scope.isLoading = true;
            activeWFilters = [];
            var index = activeWFilters.indexOf(dictonaryName);
            if (index === -1) {
                activeWFilters.push(dictonaryName);
            }
            activeWFilters[dictonaryName] = $scope.sourceselected;
            search();
        }
        var moreLikeThis = function (id) {
            $http.get("/api/morelikethis?id=" + encodeURIComponent(id)).then(function (data) {
                $scope.currentItem.similar = data.Results;
            });
        }

        $scope.setQuery = function (q) {
            $scope.query = q;
            $scope.suggested = {};
            search();
        }

        $scope.search = function () {
            $scope.suggested = {};
            $scope.currentItem = null;
            $scope.message = "";
            $scope.isLoading = true;
            search();
        }

        $scope.downloadFile = function (httpPath) {
        }

        $scope.getFile = function (id) {
            //1й вариант открытие без проверки
            //window.location="/api/get?id=" + encodeURIComponent(id);
            //2й вариант проверка после открытия
            //window.location("/api/get?id=" + encodeURIComponent(id)).then(function (response) {
            //    if (response.status == 204) {
            //        $scope.message = "Файл не найден";
            //    } else {
            //        //return response.data;
            //        window.location = "/api/get?id=" + encodeURIComponent(id);
            //    };
            //});
            //3й вариант проверка открытием, повторное открытие
            $http.post("/api/get?id=" + encodeURIComponent(id), { responseType: 'arraybuffer' }).then(function (response) {
                if (response.status == 204) {
                    $scope.message = "Файл не найден";
                } else {
                    window.location = "/api/get?id=" + encodeURIComponent(id);
                };
            });
            //getFile(id).then(function (response) {
            //    $scope.currentItem = response.data;
            //    try {
            //        window.location($scope.currentItem);
            //    }
            //    catch (err) { alert('Нет доступа!'); }
            //});
        };

        $scope.autocomplete = function (q) {
            var words = q.split(" ");
            var currentWord = words[words.length - 1];
            return $http.get("/api/autocomplete?q=" + encodeURIComponent(currentWord)).then(function (response) {
                return response.data;
            });
        };

        $scope.aggsSource = function () {
            return $http.get("/api/dictonarylist").then(function (response) {
                $scope.aggsSources = response.data.tableList;
                return $scope.aggsSources;
            });
        };

        $scope.aggsSource();

        var typeaheadLastValue = "";
        $scope.$watch('query', function (newVal, oldVal) {
            typeaheadLastValue = oldVal || "";
        });

        $scope.strip_html_tags = function (str) {
            if ((str === null) || (str === ''))
                return false;
            else
                str = str.toString();
            return str.replace(/<[^>]*>/g, '');
        };

        $scope.typeaheadOnSelect = function (item, model, label, event) {
            var words = typeaheadLastValue.split(" ");
            words[words.length - 1] = item;
            $scope.query = words.join(" ");

            //$scope.query = query;
        };

        $scope.open = function (size) {

            var modalInstance = $uibModal.open({
                animation: $scope.animationsEnabled,
                templateUrl: 'myModalContent.html',
                controller: 'ModalInstanceCtrl',
                size: size,
                resolve: {
                    items: function () {
                        return $scope.items;
                    }
                }
            });

            modalInstance.result.then(function (selectedItem) {
                $scope.selected = selectedItem;
            }, function () {
                $log.info('Modal dismissed at: ' + new Date());
            });
        };
    })
mainApp.filter('highlight', ['$sce', function ($sce) {
    return function (text) {
        return $sce.trustAsHtml(text)
    }
}]);
