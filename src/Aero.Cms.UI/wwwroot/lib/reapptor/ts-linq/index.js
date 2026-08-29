/******************************************************************************
Copyright (c) Microsoft Corporation.

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY
AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
PERFORMANCE OF THIS SOFTWARE.
***************************************************************************** */

function __awaiter(thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
}

const ArrayExtensions = function () {
  if (Array.prototype.all == null) {
    Array.prototype.all = function (predicate) {
      return ArrayUtility.all(this, predicate);
    };
  }
  if (Array.prototype.any == null) {
    Array.prototype.any = function (predicate) {
      return ArrayUtility.any(this, predicate);
    };
  }
  if (Array.prototype.average == null) {
    Array.prototype.average = function (selector) {
      return ArrayUtility.average(this, selector);
    };
  }
  if (Array.prototype.where == null) {
    Array.prototype.where = function (predicate) {
      return ArrayUtility.where(this, predicate);
    };
  }
  if (Array.prototype.whereAsync == null) {
    Array.prototype.whereAsync = function (predicate) {
      return ArrayUtility.whereAsync(this, predicate);
    };
  }
  if (Array.prototype.take == null) {
    Array.prototype.take = function (count) {
      return ArrayUtility.take(this, count);
    };
  }
  if (Array.prototype.takeLast == null) {
    Array.prototype.takeLast = function (count) {
      return ArrayUtility.takeLast(this, count);
    };
  }
  if (Array.prototype.takeWhile == null) {
    Array.prototype.takeWhile = function (predicate) {
      return ArrayUtility.takeWhile(this, predicate);
    };
  }
  if (Array.prototype.toDictionary == null) {
    Array.prototype.toDictionary = function (keySelector, elementSelector) {
      return ArrayUtility.toDictionary(this, keySelector, elementSelector);
    };
  }
  if (Array.prototype.toHashSet == null) {
    Array.prototype.toHashSet = function (keySelector) {
      return ArrayUtility.toHashSet(this, keySelector);
    };
  }
  if (Array.prototype.single == null) {
    Array.prototype.single = function (predicate, defaultValue) {
      return ArrayUtility.single(this, predicate, defaultValue);
    };
  }
  if (Array.prototype.singleOrDefault == null) {
    Array.prototype.singleOrDefault = function (predicate, defaultValue) {
      return ArrayUtility.singleOrDefault(this, predicate, defaultValue);
    };
  }
  if (Array.prototype.skip == null) {
    Array.prototype.skip = function (count) {
      return ArrayUtility.skip(this, count);
    };
  }
  if (Array.prototype.skipLast == null) {
    Array.prototype.skipLast = function (count) {
      return ArrayUtility.skipLast(this, count);
    };
  }
  if (Array.prototype.skipWhile == null) {
    Array.prototype.skipWhile = function (predicate) {
      return ArrayUtility.skipWhile(this, predicate);
    };
  }
  if (Array.prototype.select == null) {
    Array.prototype.select = function (selector) {
      return ArrayUtility.select(this, selector);
    };
  }
  if (Array.prototype.selectMany == null) {
    Array.prototype.selectMany = function (collectionSelector) {
      return ArrayUtility.selectMany(this, collectionSelector);
    };
  }
  if (Array.prototype.groupBy == null) {
    Array.prototype.groupBy = function (keySelector, elementSelector) {
      return ArrayUtility.groupBy(this, keySelector, elementSelector);
    };
  }
  if (Array.prototype.remove == null) {
    Array.prototype.remove = function (item) {
      ArrayUtility.remove(this, item);
    };
  }
  if (Array.prototype.removeAt == null) {
    Array.prototype.removeAt = function (index) {
      ArrayUtility.removeAt(this, index);
    };
  }
  if (Array.prototype.max == null) {
    Array.prototype.max = function (keySelector = null) {
      return ArrayUtility.max(this, keySelector);
    };
  }
  if (Array.prototype.maxValue == null) {
    Array.prototype.maxValue = function (keySelector) {
      return ArrayUtility.maxValue(this, keySelector);
    };
  }
  if (Array.prototype.min == null) {
    Array.prototype.min = function (keySelector = null) {
      return ArrayUtility.min(this, keySelector);
    };
  }
  if (Array.prototype.minValue == null) {
    Array.prototype.minValue = function (keySelector) {
      return ArrayUtility.minValue(this, keySelector);
    };
  }
  if (Array.prototype.sum == null) {
    Array.prototype.sum = function (selector) {
      return ArrayUtility.sum(this, selector);
    };
  }
  if (Array.prototype.count == null) {
    Array.prototype.count = function (predicate) {
      return ArrayUtility.count(this, predicate);
    };
  }
  if (Array.prototype.contains == null) {
    Array.prototype.contains = function (value) {
      return ArrayUtility.contains(this, value);
    };
  }
  if (Array.prototype.chunk == null) {
    Array.prototype.chunk = function (size) {
      return ArrayUtility.chunk(this, size);
    };
  }
  if (Array.prototype.split == null) {
    Array.prototype.split = function (count) {
      return ArrayUtility.split(this, count);
    };
  }
  if (Array.prototype.distinct == null) {
    Array.prototype.distinct = function (predicate) {
      return ArrayUtility.distinct(this, predicate);
    };
  }
  if (Array.prototype.sortBy == null) {
    Array.prototype.sortBy = function (keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6) {
      ArrayUtility.sortBy(this, keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6);
    };
  }
  if (Array.prototype.sortByDescending == null) {
    Array.prototype.sortByDescending = function (keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6) {
      ArrayUtility.sortByDescending(this, keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6);
    };
  }
  if (Array.prototype.forEachAsync == null) {
    Array.prototype.forEachAsync = function (predicate) {
      return ArrayUtility.forEachAsync(this, predicate);
    };
  }
  if (Array.prototype.except == null) {
    Array.prototype.except = function (except, comparer) {
      return ArrayUtility.except(this, except, comparer);
    };
  }
  if (Array.prototype.first == null) {
    Array.prototype.first = function (predicate, defaultValue) {
      return ArrayUtility.first(this, predicate, defaultValue);
    };
  }
  if (Array.prototype.firstOrDefault == null) {
    Array.prototype.firstOrDefault = function (predicate, defaultValue) {
      return ArrayUtility.firstOrDefault(this, predicate, defaultValue);
    };
  }
  if (Array.prototype.last == null) {
    Array.prototype.last = function (predicate, defaultValue) {
      return ArrayUtility.last(this, predicate, defaultValue);
    };
  }
  if (Array.prototype.lastOrDefault == null) {
    Array.prototype.lastOrDefault = function (predicate, defaultValue) {
      return ArrayUtility.lastOrDefault(this, predicate, defaultValue);
    };
  }
  if (Array.prototype.repeat == null) {
    Array.prototype.repeat = function (element, count) {
      return ArrayUtility.repeat(element, count);
    };
  }
  if (Array.prototype.insert == null) {
    Array.prototype.insert = function (item, index) {
      ArrayUtility.insert(this, item, index);
    };
  }
};
ArrayExtensions();

class LinqSettings {
  constructor() {
    /**
     * Supported formats:
     *   "yyyy-MM-ddThh:mm:ss.sssZ"
     *   "yyyy-MM-ddThh:mm:ss+hh:mm"
     * Examples:
     *   "2019-09-25T16:00:20.817Z"
     *   "2019-09-25T16:00:20.817"
     *   "2019-09-25"
     *   "2019-09-24T00:00:00"
     *   "2019-09-24T00:00:00Z"
     *   "2019-10-14T21:00:00.000Z"
     *   "2019-10-16T00:00:00+03:00"
     */
    this.stringToDateCastRegex = /^(-?(?:[1-9][0-9]*)?[0-9]{4})-(1[0-2]|0[1-9])-(3[01]|0[1-9]|[12][0-9])T(2[0-3]|[01][0-9]):([0-5][0-9]):([0-5][0-9])(.[0-9]+)?(:([0-5][0-9]))?(Z)?$/;
    this.stringToDateCastResolver = date => {
      return date != null && (typeof date === "object" && date.constructor === Date || typeof date === "string" && !!date.match(this.stringToDateCastRegex));
    };
    this.stringToDateCastEnabled = true;
  }
}

class Linq {
  static init() {}
}
Linq.settings = new LinqSettings();

class ArrayUtility {
  static compareDateType(x, y, ascending = true) {
    x = typeof x === "string" ? new Date(x) : x;
    y = typeof y === "string" ? new Date(y) : y;
    const xValue = x.valueOf();
    const yValue = y.valueOf();
    if (xValue > yValue) {
      return ascending ? 1 : -1;
    }
    if (xValue < yValue) {
      return ascending ? -1 : 1;
    }
    return 0;
  }
  static invokeSortBy(source, ascending, keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6) {
    const greaterThen = ascending ? 1 : -1;
    const lessThen = ascending ? -1 : 1;
    const compare = (keySelector, x, y) => {
      const xKey = keySelector ? keySelector(x) : x;
      const yKey = keySelector ? keySelector(y) : y;
      if (Linq.settings.stringToDateCastEnabled && Linq.settings.stringToDateCastResolver(xKey)) {
        return this.compareDateType(xKey, yKey, ascending);
      }
      return xKey > yKey ? greaterThen : xKey < yKey ? lessThen : 0;
    };
    const comparator = (x, y) => {
      let value = compare(keySelector1, x, y);
      if (value === 0 && keySelector2) {
        value = compare(keySelector2, x, y);
        if (value === 0 && keySelector3) {
          value = compare(keySelector3, x, y);
          if (value === 0 && keySelector4) {
            value = compare(keySelector4, x, y);
            if (value === 0 && keySelector5) {
              value = compare(keySelector5, x, y);
              if (value === 0 && keySelector6) {
                value = compare(keySelector6, x, y);
              }
            }
          }
        }
      }
      return value;
    };
    source.sort(comparator);
  }
  static all(items, predicate) {
    return items.every(predicate);
  }
  static any(items, predicate) {
    return predicate ? items.some(predicate) : items.length > 0;
  }
  static where(items, predicate) {
    return items.filter(predicate);
  }
  static whereAsync(items, predicate) {
    return __awaiter(this, void 0, void 0, function* () {
      const result = [];
      const length = items.length;
      for (let i = 0; i < length; i++) {
        const item = items[i];
        const passed = yield predicate(item);
        if (passed) {
          result.push(item);
        }
      }
      return result;
    });
  }
  static select(source, selector) {
    const result = [];
    const length = source.length;
    for (let i = 0; i < length; i++) {
      const item = source[i];
      const resultItem = selector(item, i);
      result.push(resultItem);
    }
    return result;
  }
  static selectMany(items, collectionSelector) {
    const result = [];
    const length = items.length;
    for (let i = 0; i < length; i++) {
      const subItems = collectionSelector(items[i]);
      result.push(...subItems);
    }
    return result;
  }
  static chunk(items, size) {
    if (size < 1) throw new Error(`Size "${size}" out of range, must be at least 1 or greater.`);
    const result = [];
    const copy = [...items];
    while (copy.length) {
      result.push(copy.splice(0, size));
    }
    return result;
  }
  static split(items, count) {
    if (count < 1) throw new Error(`Count "${count}" out of range, must be at least 1 or greater.`);
    const delta = items.length / count;
    let size = Math.trunc(delta);
    if (delta > size || size === 0) {
      size = size + 1;
    }
    return this.chunk(items, size);
  }
  static take(items, count) {
    if (count < 0) {
      count = 0;
    }
    let length = items.length;
    if (count >= 0 && count < length) {
      length = count;
    }
    const result = new Array(length);
    for (let i = 0; i < length; i++) {
      result[i] = items[i];
    }
    return result;
  }
  static takeLast(items, count) {
    if (count < 0) {
      count = 0;
    }
    const sourceLength = items.length;
    const length = count >= 0 && count < sourceLength ? count : sourceLength;
    const result = new Array(length);
    const prefix = sourceLength - length;
    for (let i = 0; i < length; i++) {
      result[i] = items[prefix + i];
    }
    return result;
  }
  static takeWhile(items, predicate) {
    const result = [];
    const length = items.length;
    for (let i = 0; i < length; i++) {
      const item = items[i];
      const valid = predicate(item, i);
      if (!valid) {
        break;
      }
      result.push(item);
    }
    return result;
  }
  static toDictionary(items, keySelector, elementSelector) {
    const map = new Map();
    let length = items.length;
    for (let i = 0; i < length; i++) {
      const item = items[i];
      const key = keySelector ? keySelector(item, i) : item;
      const element = elementSelector ? elementSelector(item, i) : item;
      const collection = map.get(key);
      if (!collection) {
        map.set(key, [element]);
      } else {
        collection.push(element);
      }
    }
    return map;
  }
  static toHashSet(items, keySelector) {
    const set = new Set();
    const hasKeySelector = keySelector != null;
    let length = items.length;
    for (let i = 0; i < length; i++) {
      const item = items[i];
      const key = hasKeySelector ? keySelector(item, i) : item;
      set.add(key);
    }
    return set;
  }
  static single(items, predicate, defaultValue) {
    const item = ArrayUtility.singleOrDefault(items, predicate, defaultValue);
    if (item == null) {
      const error = predicate ? `No item found matching the specified predicate.` : `The source sequence is empty.`;
      throw new Error(error);
    }
    return item;
  }
  static singleOrDefault(items, predicate, defaultValue) {
    const length = items.length;
    let result = null;
    if (predicate) {
      for (let i = 0; i < length; i++) {
        const item = items[i];
        if (predicate(item)) {
          if (result != null) throw new Error(`The input sequence contains more than one element.`);
          result = item;
        }
      }
    } else if (length == 1) {
      result = items[0];
    } else if (length > 1) {
      throw new Error(`The input sequence contains more than one element.`);
    }
    return result != null ? result : defaultValue !== null && defaultValue !== void 0 ? defaultValue : null;
  }
  static skip(items, count) {
    if (count < 0) {
      count = 0;
    }
    const length = items.length;
    const firstIndex = count < length ? count : length;
    const newLength = length - firstIndex;
    const result = new Array(newLength);
    for (let dest = 0, source = firstIndex; dest < newLength; dest++, source++) {
      result[dest] = items[source];
    }
    return result;
  }
  static skipLast(items, count) {
    if (count < 0) {
      count = 0;
    }
    let sourceLength = items.length;
    const length = count <= 0 ? sourceLength : count < sourceLength ? sourceLength - count : 0;
    const result = new Array(length);
    for (let i = 0; i < length; i++) {
      result[i] = items[i];
    }
    return result;
  }
  static skipWhile(items, predicate) {
    const length = items.length;
    let index = 0;
    for (let i = 0; i < length; i++) {
      const skip = predicate(items[i], i);
      if (!skip) {
        break;
      }
      index++;
    }
    const result = [];
    for (let i = index; i < length; i++) {
      result.push(items[i]);
    }
    return result;
  }
  static first(items, predicate, defaultValue) {
    const item = ArrayUtility.firstOrDefault(items, predicate, defaultValue);
    if (item == null) {
      const error = predicate ? `No item found matching the specified predicate.` : `The source sequence is empty.`;
      throw new Error(error);
    }
    return item;
  }
  static firstOrDefault(items, predicate, defaultValue) {
    const length = items.length;
    if (predicate) {
      for (let i = 0; i < length; i++) {
        const item = items[i];
        if (predicate(item)) {
          return item;
        }
      }
    } else if (length > 0) {
      return items[0];
    }
    return defaultValue != null ? defaultValue : null;
  }
  static last(items, predicate, defaultValue) {
    const item = ArrayUtility.lastOrDefault(items, predicate, defaultValue);
    if (item == null) {
      const error = predicate ? "No item found matching the specified predicate." : "The source sequence is empty.";
      throw new Error(error);
    }
    return item;
  }
  static lastOrDefault(items, predicate, defaultValue) {
    const length = items.length;
    if (predicate) {
      for (let i = length - 1; i >= 0; i--) {
        const item = items[i];
        if (predicate(item)) {
          return item;
        }
      }
    } else if (length > 0) {
      return items[length - 1];
    }
    return defaultValue !== null && defaultValue !== void 0 ? defaultValue : null;
  }
  static forEachAsync(items, predicate) {
    return __awaiter(this, void 0, void 0, function* () {
      const promises = items.map(item => predicate(item));
      yield Promise.all(promises);
    });
  }
  static except(items, except, comparer) {
    const xLength = items.length;
    if (xLength == 0) {
      return [];
    }
    const yLength = except.length;
    if (yLength == 0) {
      return [...items];
    }
    if (comparer == null) {
      const result = [];
      const valueSet = new Set(except);
      for (let i = 0; i < xLength; i++) {
        const item = items[i];
        if (!valueSet.has(item)) {
          valueSet.add(item);
          result.push(item);
        }
      }
      return result;
    }
    const result = [];
    for (let i = 0; i < xLength; i++) {
      const item = items[i];
      let exists = false;
      for (let j = 0; j < yLength; j++) {
        const yItem = except[j];
        if (comparer(item, yItem)) {
          exists = true;
          break;
        }
      }
      if (!exists) {
        result.push(item);
      }
    }
    return result;
  }
  static groupBy(items, keySelector, elementSelector) {
    const map = this.toDictionary(items, keySelector, elementSelector);
    return Array.from(map.values());
  }
  static remove(items, item) {
    if (typeof item === "function") {
      item = items.where(item);
    }
    if (Array.isArray(item)) {
      const length = item.length;
      for (let i = 0; i < length; i++) {
        ArrayUtility.remove(items, item[i]);
      }
    } else {
      const index = items.indexOf(item);
      if (index !== -1) {
        items.splice(index, 1);
      }
    }
  }
  static removeAt(items, index) {
    if (index < 0 || index >= items.length) {
      const message = items.length > 0 ? `Array index "${index}" out of range, can be in [0..${items.length}].` : `Array index "${index}" out of range, array is empty.`;
      throw new Error(message);
    }
    items.splice(index, 1);
  }
  static max(items, keySelector = null) {
    const length = items.length;
    if (length === 0) throw new Error("The source sequence is empty.");
    keySelector = keySelector || (item => item);
    let maxItem = items[0];
    let maxValue = keySelector(maxItem);
    for (let i = 1; i < length; i++) {
      const item = items[i];
      const value = keySelector(item);
      if (value > maxValue) {
        maxValue = value;
        maxItem = item;
      }
    }
    return maxItem;
  }
  static maxValue(items, keySelector) {
    return keySelector(ArrayUtility.max(items, keySelector));
  }
  static min(items, keySelector = null) {
    const length = items.length;
    if (length === 0) throw new Error(`The source sequence is empty.`);
    keySelector = keySelector || (item => item);
    let minItem = items[0];
    let minValue = keySelector(minItem);
    for (let i = 1; i < length; i++) {
      const item = items[i];
      const value = keySelector(item);
      if (value < minValue) {
        minValue = value;
        minItem = item;
      }
    }
    return minItem;
  }
  static minValue(items, predicate) {
    return predicate(this.min(items, predicate));
  }
  static sum(items, selector) {
    var _a;
    let sum = 0;
    if (items != null) {
      const length = items.length;
      for (let i = 0; i < length; i++) {
        const item = items[i];
        const value = selector ? (_a = selector(item)) !== null && _a !== void 0 ? _a : 0 : item;
        sum = sum + value;
      }
    }
    return sum;
  }
  static count(items, predicate) {
    let count = 0;
    if (items) {
      if (predicate) {
        items.forEach((item, index) => count += predicate(item, index) ? 1 : 0);
      } else {
        count = items.length;
      }
    }
    return count;
  }
  static contains(items, value) {
    return items.includes(value);
  }
  static distinct(items, predicate) {
    const result = [];
    const length = items.length;
    if (length > 0) {
      const set = new Set();
      for (let i = 0; i < length; i++) {
        const item = items[i];
        const key = predicate ? predicate(item) : item;
        if (!set.has(key)) {
          set.add(key);
          result.push(items[i]);
        }
      }
    }
    return result;
  }
  static repeat(element, count) {
    const items = new Array(count);
    for (let i = 0; i < count; i++) {
      items[i] = element;
    }
    return items;
  }
  static insert(items, item, index) {
    if (index == null) {
      index = 0;
    } else {
      if (index < 0 || index > items.length) throw new Error(`Array index "${index}" out of range, can be in [0..${items.length}].`);
    }
    if (Array.isArray(item)) {
      items.splice(index, 0, ...item);
    } else {
      items.splice(index, 0, item);
    }
  }
  static average(items, selector) {
    const length = items.length;
    if (length === 0) throw new Error(`The source sequence is empty.`);
    const sum = this.sum(items, selector);
    return sum / length;
  }
  static sortBy(source, keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6) {
    this.invokeSortBy(source, true, keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6);
  }
  static sortByDescending(source, keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6) {
    this.invokeSortBy(source, false, keySelector1, keySelector2, keySelector3, keySelector4, keySelector5, keySelector6);
  }
}

export { ArrayExtensions, ArrayUtility, Linq };
